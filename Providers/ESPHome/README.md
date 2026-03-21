# ESPHome Provider

This provider allows you to control ESPHome devices via their integrated REST API.

## Requirements
The `web_server` component must be enabled in your ESPHome configuration (`.yaml`) to allow HTTP access:

```yaml
web_server:
  port: 80
```

## Configuration
*   **Host:** The IP address or hostname of the ESPHome device.
*   **Entity ID:** The name of the switch object defined in ESPHome (e.g., `switch-stall-outdoor-lighting`).

## How it works
The provider communicates with the REST API of the ESPHome web server. It sends `POST` requests to `/switch/<id>/turn_on` or `turn_off` and queries the state via `/switch/<id>`.
