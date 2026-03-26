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

## Timer Support (Home Assistant Script/Timer Provider)

Standard Home Assistant `switch` entities do not natively support custom variables like N.I.N.A.'s "Delay" parameter. 
The **"Home Assistant (Script/Timer)"** provider bridges this gap by invoking a Home Assistant script while passing the target state and delay as variables.

### Key Logic: Polling vs. Execution

Since Home Assistant scripts are transient (they return to an `off` state immediately after starting), they cannot be used to reliably track the power state of your equipment. This provider implements a dual-entity approach:

1.  **Actual State Entity ID (Polling)**: This is a real, persistent Home Assistant entity (e.g., a `switch`, `light`, or `binary_sensor`) that N.I.N.A. uses to show the ON/OFF state in the UI.
2.  **Script Entity ID (Action)**: This is the Home Assistant script that N.I.N.A. invokes when you toggle the switch.

### Universal vs. Specialized Scripts

The plugin passes three variables to the script: `real_entity_id`, `target_state` (`on`/`off`), and `delay_seconds`.

*   **Universal Use-Case**: You can use a single script for many N.I.N.A. switches by using the `real_entity_id` variable in your YAML code to dynamically target different devices.
*   **Specialized Use-Case**: For complex sequences (e.g., closing a roof, then shutting down a PC, then cutting power), you can create a custom script. In this case, you can ignore the `real_entity_id` variable in your YAML. N.I.N.A. will still use the **Actual State Entity ID** to confirm once the entire sequence has finished (e.g., by polling the final power relay).

### Creating the Universal Script in Home Assistant

1. Open Home Assistant and go to **Settings** -> **Automations & Tags** -> **Scripts**.
2. Click **Add Script** -> **Create new script**.
3. In the top right corner, click the three-dot menu icon (⋮) and choose **Edit in YAML**.
4. Paste the following blueprint code:

```yaml
alias: "N.I.N.A. Universal Switch Timer"
mode: parallel
sequence:
  - delay:
      seconds: "{{ delay_seconds | default(0) }}"
  - service: >
      {% if target_state == 'on' %} switch.turn_on
      {% else %} switch.turn_off
      {% endif %}
    target:
      entity_id: "{{ real_entity_id }}"
```

### Configuring N.I.N.A.

1. Add a new Smart Switch in N.I.N.A.
2. Set the Provider to **Home Assistant (Script/Timer)**.
3. **Actual State Entity ID**: Enter the real switch ID (e.g., `switch.observatory_power`).
4. **Script Entity ID**: Enter your script ID (e.g., `script.n_i_n_a_universal_switch_timer`).
5. Set `Turn On Service` and `Turn Off Service` to `turn_on` in the Expert settings (N.I.N.A. must "start" the script in both directions).
