extends Node

var player = preload("res://Player.tscn") setget , player_get
var console = preload("res://ui/Console.tscn") setget, console_get
var debug_overlay = preload("res://ui/DebugOverlay.tscn") setget, debug_overlay_get
var login_screen = preload("res://ui/LoginScreen.tscn") setget, login_screen_get

func player_get() -> PackedScene:
    return player

func console_get() -> PackedScene:
    return console

func debug_overlay_get() -> PackedScene:
    return debug_overlay

func login_screen_get() -> PackedScene:
    return login_screen
