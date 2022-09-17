extends Node

var player = preload("res://Player.tscn") setget , player_get
var console = preload("res://ui/Console.tscn") setget, console_get
var debug_overlay = preload("res://ui/DebugOverlay.tscn") setget, debug_overlay_get
var login_screen = preload("res://ui/LoginScreen.tscn") setget, login_screen_get
var server_manager = preload("res://Network/Server.tscn") setget, server_get
var client_manager = preload("res://Network/Client.tscn") setget, client_get
var client_master = preload("res://Network/ClientMaster.tscn") setget, client_master_get

func player_get() -> PackedScene:
    return player

func console_get() -> PackedScene:
    return console

func debug_overlay_get() -> PackedScene:
    return debug_overlay

func login_screen_get() -> PackedScene:
    return login_screen

func client_get() -> PackedScene:
    return client_manager

func server_get() -> PackedScene:
    return server_manager

func client_master_get() -> PackedScene:
    return client_master
