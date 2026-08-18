package main

import (
	"bufio"
	"flag"
	"fmt"
	"io"
	"net"
	"net/url"
	"os"
	"os/exec"
	"os/signal"
	"strings"
	"syscall"
	"time"

	"github.com/Microsoft/go-winio"
	"github.com/xjasonlyu/tun2socks/v2/engine"
	"golang.org/x/net/proxy"
)

func main() {
	proxyFlag := flag.String("proxy", "", "SOCKS5 proxy URL (e.g. socks5://airtun:pin@192.168.43.1:10808)")
	tunNameFlag := flag.String("tun-name", "AirTun", "TUN interface name")
	tunAddrFlag := flag.String("tun-addr", "10.254.1.2/24", "TUN interface address")
	pipeFlag := flag.String("pipe", "", "Named pipe name for parent IPC")
	flag.Parse()

	if *proxyFlag == "" {
		fmt.Fprintln(os.Stderr, "Error: -proxy argument is required")
		os.Exit(1)
	}

	var pipeConn net.Conn
	var pipeWriter *bufio.Writer
	if *pipeFlag != "" {
		pipePath := `\\.\pipe\` + *pipeFlag
		var err error
		for i := 0; i < 50; i++ {
			timeout := 500 * time.Millisecond
			pipeConn, err = winio.DialPipe(pipePath, &timeout)
			if err == nil {
				break
			}
			time.Sleep(100 * time.Millisecond)
		}
		if pipeConn != nil {
			pipeWriter = bufio.NewWriter(pipeConn)
		} else {
			fmt.Fprintf(os.Stderr, "Could not open pipe %s: %v\n", pipePath, err)
		}
	}

	sendLine := func(line string) {
		fmt.Println(line)
		if pipeWriter != nil {
			pipeWriter.WriteString(line + "\n")
			pipeWriter.Flush()
		}
	}

	// Parse proxy host for routing bypass
	proxyUrl, err := url.Parse(*proxyFlag)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Invalid proxy URL: %v\n", err)
		os.Exit(1)
	}
	var proxyHost string
	proxyHost, _, _ = net.SplitHostPort(proxyUrl.Host)
	if proxyHost == "" {
		proxyHost = proxyUrl.Host
	}

	// Start tun2socks engine
	tunDevice := fmt.Sprintf("tun://%s", *tunNameFlag)
	key := &engine.Key{
		Device:   tunDevice,
		Proxy:    *proxyFlag,
		MTU:      1500,
		LogLevel: "silent",
	}

	engine.Insert(key)
	engine.Start()

	// Wait for WinTun adapter creation
	time.Sleep(500 * time.Millisecond)

	// Configure TUN IP Address
	ipParts := strings.Split(*tunAddrFlag, "/")
	tunIP := "10.254.1.2"
	tunMask := "255.255.255.0"
	if len(ipParts) > 0 && ipParts[0] != "" {
		tunIP = ipParts[0]
	}

	// Find physical default gateway for bypass route
	defaultGW := getDefaultGateway()

	// 1. Assign IP address to AirTun adapter
	exec.Command("netsh", "interface", "ip", "set", "address", fmt.Sprintf("name=\"%s\"", *tunNameFlag), "static", tunIP, tunMask, "none").Run()

	// 2. Set DNS servers on AirTun adapter to our local TCP-tunneled DNS forwarder (tunIP:53)
	exec.Command("netsh", "interface", "ip", "set", "dns", fmt.Sprintf("name=\"%s\"", *tunNameFlag), "static", tunIP).Run()

	// 3. Bypass direct route to phone host so tunnel traffic doesn't loop
	if proxyHost != "" && proxyHost != "127.0.0.1" && proxyHost != "localhost" {
		if defaultGW != "" {
			exec.Command("route", "add", proxyHost, "mask", "255.255.255.255", defaultGW, "metric", "1").Run()
		} else {
			exec.Command("route", "add", proxyHost, "mask", "255.255.255.255", "0.0.0.0", "metric", "1").Run()
		}
	}

	// 4. Start local DNS Forwarder over SOCKS5 TCP
	dnsStop, err := startDNSForwarder(tunIP+":53", proxyUrl)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Warning: failed to start local DNS forwarder: %v\n", err)
	}

	// 5. Add dual /1 default routes on-link to direct all system IPv4 traffic into the TUN adapter
	exec.Command("route", "add", "0.0.0.0", "mask", "128.0.0.0", tunIP, "metric", "1").Run()
	exec.Command("route", "add", "128.0.0.0", "mask", "128.0.0.0", tunIP, "metric", "1").Run()

	// Signal READY to parent process
	sendLine("READY")

	// Monitor pipe and OS signals for graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, os.Interrupt, syscall.SIGTERM)

	done := make(chan struct{})
	if pipeConn != nil {
		go func() {
			scanner := bufio.NewScanner(pipeConn)
			for scanner.Scan() {
				// Parent is alive
			}
			close(done)
		}()
	}

	select {
	case <-sigChan:
	case <-done:
	}

	// Cleanup all added routes and stop engine
	if dnsStop != nil {
		dnsStop()
	}
	exec.Command("route", "delete", "0.0.0.0", "mask", "128.0.0.0").Run()
	exec.Command("route", "delete", "128.0.0.0", "mask", "128.0.0.0").Run()
	if proxyHost != "" {
		exec.Command("route", "delete", proxyHost).Run()
	}
	engine.Stop()
	if pipeConn != nil {
		pipeConn.Close()
	}
}

func startDNSForwarder(listenAddr string, proxyUrl *url.URL) (func(), error) {
	udpAddr, err := net.ResolveUDPAddr("udp", listenAddr)
	if err != nil {
		return nil, err
	}

	conn, err := net.ListenUDP("udp", udpAddr)
	if err != nil {
		return nil, err
	}

	dialer, err := proxy.FromURL(proxyUrl, proxy.Direct)
	if err != nil {
		conn.Close()
		return nil, err
	}

	stopChan := make(chan struct{})

	go func() {
		buf := make([]byte, 4096)
		for {
			select {
			case <-stopChan:
				return
			default:
			}

			conn.SetReadDeadline(time.Now().Add(500 * time.Millisecond))
			n, clientAddr, err := conn.ReadFromUDP(buf)
			if err != nil {
				continue
			}

			query := make([]byte, n)
			copy(query, buf[:n])

			go func(q []byte, cAddr *net.UDPAddr) {
				resp, err := queryDNSOverTCP(dialer, "1.1.1.1:53", q)
				if err != nil {
					resp, err = queryDNSOverTCP(dialer, "8.8.8.8:53", q)
				}
				if err == nil && len(resp) > 0 {
					conn.WriteToUDP(resp, cAddr)
				}
			}(query, clientAddr)
		}
	}()

	cleanup := func() {
		close(stopChan)
		conn.Close()
	}
	return cleanup, nil
}

func queryDNSOverTCP(dialer proxy.Dialer, upstream string, query []byte) ([]byte, error) {
	tcpConn, err := dialer.Dial("tcp", upstream)
	if err != nil {
		return nil, err
	}
	defer tcpConn.Close()

	tcpConn.SetDeadline(time.Now().Add(4 * time.Second))

	qLen := uint16(len(query))
	lenBuf := []byte{byte(qLen >> 8), byte(qLen & 0xFF)}

	if _, err := tcpConn.Write(append(lenBuf, query...)); err != nil {
		return nil, err
	}

	respLenBuf := make([]byte, 2)
	if _, err := io.ReadFull(tcpConn, respLenBuf); err != nil {
		return nil, err
	}

	respLen := (uint16(respLenBuf[0]) << 8) | uint16(respLenBuf[1])
	resp := make([]byte, respLen)
	if _, err := io.ReadFull(tcpConn, resp); err != nil {
		return nil, err
	}

	return resp, nil
}

func getDefaultGateway() string {
	out, err := exec.Command("powershell", "-NoProfile", "-Command", "(Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Sort-Object RouteMetric | Select-Object -First 1).NextHop").Output()
	if err == nil {
		gw := strings.TrimSpace(string(out))
		if gw != "" && net.ParseIP(gw) != nil {
			return gw
		}
	}
	return ""
}


