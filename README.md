# Under the Castle UI for LibreKO

The Under the Castle generation of the Knight Online interface as a UI theme plugin for the
[LibreKO](https://github.com/ZeusAFK/LibreKO) client. A fan-made work, distributed on its own.

## Install

1. Download the latest archive from the Releases page.
2. In the client, open Settings → Plugins → *Open plugins folder* and extract the archive there.
3. Restart, enable **Under the Castle UI** in Settings → Plugins, restart again.

Nothing to build. Only one UI theme can be enabled at a time.

## Build from source

```
dotnet build -p:LibreKOClientDir=<folder containing LibreKO.dll>
```

`package.ps1` builds Release and writes the release archive to `dist/`.

## License

AGPL-3.0, see `LICENSE`.
