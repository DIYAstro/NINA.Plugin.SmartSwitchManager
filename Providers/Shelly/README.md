# Shelly Provider

This provider supports Shelly devices of Generation 1 (Gen1) and Generation 2/3 (Gen2/Gen3, e.g., Plus/Pro series).

## Supported Models
*   **Gen 1:** Shelly 1, Shelly 1PM, Shelly Plug, Shelly Plug S, Shelly 2.5 etc.
*   **Gen 2/3:** Shelly Plus 1, Shelly Plus Plug S, Shelly Pro 4PM etc.

## Configuration
*   **Host:** The IP address of the Shelly device.
*   **Username / Password:** (Optional) If you have enabled restricted access (REST API Auth) in the Shelly web interface.
*   **Channel:** The index of the relay (starting at 0).

## Scanner
The Shelly scanner automatically detects all relays in the device and determines whether it is a Gen1 or Gen2 device. The configuration is automatically set up correctly.
