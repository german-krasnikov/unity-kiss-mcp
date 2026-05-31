"""Cycle 7b — TCP/OS robustness: socket options tests."""
import socket
import sys

import pytest


@pytest.mark.asyncio
async def test_socket_options_set_on_connect(mock_unity_server):
    from unity_mcp.bridge import UnityBridge
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()
    sock = bridge._writer.get_extra_info("socket")
    # Non-zero means enabled (macOS returns 4, Linux returns 1)
    assert sock.getsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY) != 0
    assert sock.getsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE) != 0
    await bridge.close()


@pytest.mark.asyncio
async def test_socket_options_set_on_reconnect(mock_unity_server):
    from unity_mcp.bridge import UnityBridge
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()
    await bridge._reconnect()
    sock = bridge._writer.get_extra_info("socket")
    assert sock.getsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY) != 0
    assert sock.getsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE) != 0
    await bridge.close()


@pytest.mark.skipif(sys.platform != "darwin", reason="macOS-specific")
@pytest.mark.asyncio
async def test_socket_options_platform_specific_darwin(mock_unity_server):
    from unity_mcp.bridge import UnityBridge
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()
    sock = bridge._writer.get_extra_info("socket")
    assert sock.getsockopt(socket.IPPROTO_TCP, 0x10) == 60
    await bridge.close()


# FIX 2: verify standard keepalive constants
@pytest.mark.skipif(sys.platform != "darwin", reason="macOS-specific")
@pytest.mark.asyncio
async def test_keepalive_values_standard(mock_unity_server):
    """Keepalive: idle=60, interval=10 — relaxed for macOS App Nap tolerance."""
    from unity_mcp.bridge import UnityBridge, _TCP_KEEPALIVE_DARWIN, _TCP_KEEPINTVL_DARWIN
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()
    sock = bridge._writer.get_extra_info("socket")
    assert sock.getsockopt(socket.IPPROTO_TCP, _TCP_KEEPALIVE_DARWIN) == 60
    assert sock.getsockopt(socket.IPPROTO_TCP, _TCP_KEEPINTVL_DARWIN) == 10
    await bridge.close()


def test_socket_options_no_extra_info_socket_safe():
    from unity_mcp.bridge import _apply_socket_options
    _apply_socket_options(None)  # must not raise
