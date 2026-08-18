package main

import (
	"bufio"
	"flag"
	"fmt"
	"net"
	"net/url"
	"os"
	"os/exec"
	"os/signal"
	"strings"
	"syscall"
	"time"

	"github.com/xjasonlyu/tun2socks/v2/engine"
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

	var pipeConn *os.File
	var pipeWriter *bufio.Writer
	if *pipeFlag != "" {
		pipePath := `\\.\pipe\` + *pipeFlag
		var err error
		pipeConn, err = os.OpenFile(pipePath, os.O_RDWR, 0)
		if err != nil {
			fmt.Fprintf(os.Stderr, "Could not open pipe %s: %v\n", pipePath, err)
		} else {
			pipeWriter = bufio.NewWriter(pipeConn)
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
	var proxyHost string
	if err == nil {
		proxyHost, _, _ = net.SplitHostPort(proxyUrl.Host)
		if proxyHost == "" {
			proxyHost = proxyUrl.Host
		}
	}

	// Start tun2socks engine
	tunDevice := fmt.Sprintf("wintun://%s", *tunNameFlag)
	key := &engine.Key{
		Device:   tunDevice,
		Proxy:    *proxyFlag,
		MTU:      1500,
		LogLevel: "silent",
	}

	engine.Insert(key)
	engine.Start()

	// Wait briefly for WinTun adapter creation
	time.Sleep(500 * time.Millisecond)

	// Configure TUN IP Address
	ipParts := strings.Split(*tunAddrFlag, "/")
	tunIP := "10.254.1.2"
	tunMask := "255.255.255.0"
	if len(ipParts) > 0 && ipParts[0] != "" {
		tunIP = ipParts[0]
	}
	exec.Command("netsh", "interface", "ip", "set", "address", fmt.Sprintf("name=\"%s\"", *tunNameFlag), "static", tunIP, tunMask).Run()

	// Bypass direct route to phone host so tunnel traffic doesn't loop
	if proxyHost != "" && proxyHost != "127.0.0.1" && proxyHost != "localhost" {
		exec.Command("route", "add", proxyHost, "mask", "255.255.255.255", "0.0.0.0", "metric", "1").Run()
	}

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

	// Cleanup routes and stop engine
	if proxyHost != "" {
		exec.Command("route", "delete", proxyHost).Run()
	}
	engine.Stop()
	if pipeConn != nil {
		pipeConn.Close()
	}
}
