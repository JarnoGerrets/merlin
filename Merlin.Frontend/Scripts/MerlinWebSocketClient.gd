extends Node
class_name MerlinWebSocketClient

signal connection_state_changed(state: String, detail: String)
signal response_received(response: Dictionary)
signal malformed_response(raw_message: String, detail: String)
signal socket_closed(code: int, reason: String)

const DEFAULT_URL := "ws://localhost:5000/ws"

var url := DEFAULT_URL
var _socket := WebSocketPeer.new()
var _state := "disconnected"
var _last_peer_state := WebSocketPeer.STATE_CLOSED


func _ready() -> void:
	set_process(false)


func connect_to_backend(target_url: String = DEFAULT_URL) -> void:
	url = target_url
	_socket = WebSocketPeer.new()
	_last_peer_state = WebSocketPeer.STATE_CONNECTING

	var error := _socket.connect_to_url(url)
	if error != OK:
		_set_state("error", "Could not start WebSocket connection. Error code: %s" % error)
		set_process(false)
		return

	_set_state("connecting", "Connecting to %s" % url)
	set_process(true)


func disconnect_from_backend() -> void:
	if _socket.get_ready_state() == WebSocketPeer.STATE_OPEN:
		_socket.close(1000, "Client disconnecting.")

	_set_state("disconnected", "Disconnected.")
	set_process(false)


func send_message(message: String, correlation_id: String) -> bool:
	if _socket.get_ready_state() != WebSocketPeer.STATE_OPEN:
		_set_state("error", "Cannot send message because Merlin.Backend is not connected.")
		return false

	var payload := {
		"message": message,
		"correlationId": correlation_id
	}

	var json := JSON.stringify(payload)
	var error := _socket.send_text(json)
	if error != OK:
		_set_state("error", "Failed to send WebSocket message. Error code: %s" % error)
		return false

	return true


func is_backend_connected() -> bool:
	return _socket.get_ready_state() == WebSocketPeer.STATE_OPEN


func get_connection_state() -> String:
	return _state


func _process(_delta: float) -> void:
	_socket.poll()

	var peer_state := _socket.get_ready_state()
	if peer_state != _last_peer_state:
		_handle_peer_state_changed(peer_state)
		_last_peer_state = peer_state

	while _socket.get_available_packet_count() > 0:
		var packet := _socket.get_packet()
		var raw_message := packet.get_string_from_utf8()
		_handle_raw_message(raw_message)

	if peer_state == WebSocketPeer.STATE_CLOSED:
		set_process(false)


func _handle_peer_state_changed(peer_state: int) -> void:
	match peer_state:
		WebSocketPeer.STATE_CONNECTING:
			_set_state("connecting", "Connecting to %s" % url)
		WebSocketPeer.STATE_OPEN:
			_set_state("connected", "Connected.")
		WebSocketPeer.STATE_CLOSING:
			_set_state("disconnected", "Connection closing.")
		WebSocketPeer.STATE_CLOSED:
			var code := _socket.get_close_code()
			var reason := _socket.get_close_reason()
			socket_closed.emit(code, reason)

			if _state == "connecting":
				_set_state("error", "Backend offline or connection refused.")
			else:
				_set_state("disconnected", "WebSocket closed. Code: %s Reason: %s" % [code, reason])


func _handle_raw_message(raw_message: String) -> void:
	var parsed = JSON.parse_string(raw_message)
	if typeof(parsed) != TYPE_DICTIONARY:
		malformed_response.emit(raw_message, "Response was not valid JSON object.")
		return

	response_received.emit(parsed)


func _set_state(state: String, detail: String = "") -> void:
	_state = state
	connection_state_changed.emit(state, detail)
