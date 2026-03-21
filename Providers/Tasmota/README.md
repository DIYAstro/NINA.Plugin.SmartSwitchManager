# Tasmota Provider

This provider supports all Tasmota-flashed devices accessible via the HTTP protocol.

## Configuration

*   **IP Address:** The IP address of the Tasmota device.
*   **Relay Index:** The index of the relay to control (default is `1`).
*   **User / Pass:** (Optional) If you have set web credentials for the Tasmota interface.

## Scanner
This provider supports automatic network scanning. Clicking the "Scanner" icon (magnifying glass) allows you to enter an IP address to automatically discover and import all available relays/channels on the device.

## Authentication
The provider uses Tasmota's HTTP API. If credentials are provided, they are transmitted securely as URL parameters (`user` & `password`) to the device.
