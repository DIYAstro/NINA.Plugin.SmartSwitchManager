# Home Assistant Provider

This provider allows you to control devices configured in Home Assistant directly from N.I.N.A.

## Configuration

### Basic Settings
*   **IP / Host:** The address of your Home Assistant instance (e.g., `192.168.1.10` or `homeassistant.local`).
*   **Entity ID:** The ID of the device in Home Assistant (e.g., `switch.telescope_power`).
*   **Long-Lived Token:** A long-lived access token generated within Home Assistant.

### Expert Mode (⚙️ Expert)
When Expert Mode is enabled, additional options become available:
*   **Use SSL (HTTPS):** Enables encrypted connections.
*   **Port:** Default is `8123`.
*   **Path Prefix:** Required if Home Assistant is running behind a reverse proxy with a subfolder (e.g., `/homeassistant`).
*   **Turn On/Off Service:** Allows using alternative services (e.g., `open_cover` for roof controls or `script.run` for complex logic).

## How it works
The provider uses the Home Assistant REST API. When switching, it calls the configured service for the domain specified in the Entity ID. The state is polled regularly to keep the N.I.N.A. UI up to date.
