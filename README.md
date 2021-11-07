# Relay plugin for [MCGalaxy](https://github.com/UnknownShadow200/MCGalaxy)

This makes use of the PluginMessage CPE to route messages from players to other players.

## Installing

- Download the latest dll from [GitHub Releases](https://github.com/SpiralP/MCGalaxy-Relay-Plugin/releases/latest):
  - [MCGalaxy-Relay-Plugin.dll](https://github.com/SpiralP/MCGalaxy-Relay-Plugin/releases/latest/download/MCGalaxy-Relay-Plugin.dll)
- Copy `MCGalaxy-Relay-Plugin.dll` into the `plugins` folder where `MCGalaxy.exe` lives
- Run `/plugin load MCGalaxy-Relay-Plugin` (or restart MCGalaxy)

## Compiling

Run: `dotnet build --configuration Release`

### (eventually) Used in

- https://github.com/SpiralP/classicube-cef-plugin

## Packets

```rust
struct FromClientStartPacket {
    flags: u8,
    scope: u16,
    stream_size: u16,
    data_part: [u8; 64 - 2 * 2 - 1],
}

struct FromClientContinuePacket {
    flags: u8,
    continue_data: [u8; 64 - 1],
}

// [u8; 64]
union FromClientPacket {
    start_packet: FromClientStartPacket,
    continue_packet: FromClientContinuePacket,
}

struct ToClientStartPacket {
    flags: u8,
    sender_id: u8,
    stream_size: u16,
    data_part: [u8; 64 - 2 - 1 * 2],
}

struct ToClientContinuePacket {
    flags: u8,
    continue_data: [u8; 64 - 1],
}

// [u8; 64]
union ToClientPacket {
    start_packet: ToClientStartPacket,
    continue_packet: ToClientContinuePacket,
}

// u8
// is_packet_start: 1000_0000
// stream_id: 0111_1111
struct Flags {
    // is a start packet, or is a continuation
    is_packet_start: bool,

    // TODO what am i
    stream_id: u8,
}

// u16
// byte 0: scope_id: u8,
// byte 1: scope_extra: u8,
enum Scope {
    // a single player
    Player {
        // target player id
        target_id: u8,
    },

    // all players in my map
    Map {
        // only send to those that have the same plugin that uses the same channel
        // this was sent from
        have_plugin: bool,
    },

    // all players in my server
    Server {
        have_plugin: bool,
    },
}
```
