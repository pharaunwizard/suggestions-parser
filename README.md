# SuggestionsParser

This program collects search suggestions from Google or YouTube based on provided keywords, geolocation, language, and optional SOCKS5 proxy settings. It supports multithreaded operation with proxies and customizable character sets for generating suggestions.

## Features
- Collects search suggestions from Google or YouTube.
- Supports geolocation (`gl`) and language (`hl`) parameters.
- Optional YouTube suggestions mode.
- Multithreaded operation with SOCKS5 proxy support (falls back to single-threaded if no proxies are provided).
- Customizable symbol set for generating suggestions.


## Prerequisites
- .NET SDK (version 8.0 or later recommended).
- Input files:
  - `keys.txt`: Contains the initial keywords (one per line).
  - `proxies.txt` (optional): List of SOCKS5 proxies (one per line, format: `username:password@ip:port`).
  - `symbols.txt` (optional): Custom characters for generating suggestions (one line of characters).

### Command-Line Arguments
| Short | Long       | Required | Description                                      |
|-------|------------|----------|--------------------------------------------------|
| `-g`  | `--gl`     | Yes      | Geolocation (e.g., `us` for United States). See Google/YouTube documentation for country codes. |
| `-h`  | `--hl`     | Yes      | Language (e.g., `en` for English). See Google/YouTube documentation for supported languages.    |
| `-y`  | `--youtube`| Yes      | Enable YouTube suggestions (`true` or `false`).  |

### Configuration Files
The program uses the following configuration files:

| File                | Description                                                                 | Default Behavior if Missing                        |
|---------------------|-----------------------------------------------------------------------------|----------------------------------------------------|
| `keys.txt`          | Input file with keywords (one per line).                                    | Required; program will fail if not found.          |
| `proxies.txt`       | List of SOCKS5 proxies (one per line, format: `name:password@ip:port`).     | Runs in single-threaded mode if not found.         |
| `symbols.txt`       | Custom characters for generating suggestions (single line of characters).   | Uses default symbols (`abcdefghijklmnopqrstuvwxyz`)|
| `output_keys.txt`   | Output file where collected suggestions are saved.                          | Created automatically.                             |

