# SuggestionsParser

Collects Google / YouTube search autocomplete suggestions by keywords, geolocation and language.
Two independent modes — **Simple** (classic brute force) and **Smart** (long-tail discovery) — plus SOCKS5 multithreading.

## Modes

### Simple (default)
The original behavior: each key + every character from the symbol set (`a–z` by default), a single pass.
**Flat output** — one suggestion per line. Use it for raw autocomplete expansion by first letter.

### Smart
Modifiers-based long-tail keyword discovery. Each modifier from `modifiers.txt` is applied as both
**prefix and suffix** to every seed key. No a-z mixing — modifiers only.

- **Relative frequency tiers**: every suggestion gets a `FreqScore = position*2 + words`
  (lower = more frequent). All keys are sorted and split into **value-based tertiles** — thresholds taken at the 33%/66%
  percentiles of the score — into **HF** (high) / **MF** (medium) / **LF** (low / long-tail).
- **Trend diff** (`--trend`): compares the run against the previous snapshot and writes **only the rising**
  suggestions (new since last run) to `trends.txt`.

## Prerequisites
- .NET 8 SDK (or later).
- `keys.txt` — initial seed keywords (one per line). **Required.**
- Optional: `proxies.txt`, `symbols.txt`, `modifiers.txt`.

## Command-line arguments

| Short | Long | Required | Description |
|-------|------|----------|-------------|
| `-g` | `--gl` | Yes | Geolocation, e.g. `us`. |
| `-h` | `--hl` | Yes | Language, e.g. `en`. |
| `-y` | `--youtube` | Yes | YouTube suggestions (`true` / `false`). |
| `-m` | `--mode` | No (`Simple`) | `simple` or `smart`. |
| `-t` | `--trend` | No (`false`) | **[smart]** Write only the rising suggestions (new since last run) to `trends.txt`. |

## Configuration files

| File | Description | If missing |
|------|-------------|------------|
| `keys.txt` | Seed keywords (one per line). | Required — fails. |
| `proxies.txt` | SOCKS5 proxies (`name:password@ip:port`, one per line). | Single-threaded, no proxy. |
| `symbols.txt` | Characters for the a–z pass (single line). | Default `a–z`. |
| `modifiers.txt` | **[smart]** Modifier words/phrases (one per line), applied as prefix + suffix to every seed. | Smart uses modifiers only (no a-z). |
| `output_keys.txt` | **[smart]** All suggestions with metadata (see below). | Created. |
| `HF.txt` / `MF.txt` / `LF.txt` | **[smart]** Ready-to-use keys split by relative tertile, plain text — no manual filtering. | Created. |
| `trends.txt` | **[smart, --trend]** Rising suggestions (new vs previous run). | Created when `--trend`. |
| `history/` | **[smart]** Run snapshots (used for trend diff). | Created. |

## Output format

- **Simple:** flat list — one suggestion per line, sorted alphabetically.
- **Smart:** `output_keys.txt` — all suggestions, TSV, sorted by frequency (most frequent first):
  ```
  text <TAB> score <TAB> position <TAB> words <TAB> level
  ```
  The keys are also split into three ready-to-use plain-text files by **value-based tertiles** (33%/66% score
  percentiles; equal scores never split across tiers, **no magic thresholds**): **`HF.txt`** (most frequent),
  **`MF.txt`** (medium), **`LF.txt`** (rarest / long-tail — best for low-competition targeting).

The `FreqScore = position*2 + words`. Single pass, no recursion.

## Examples

```bash
# Simple — classic a-z brute force (default mode)
dotnet run -- -g us -h en -y true

# Smart — modifiers + a-z + frequency tiers
dotnet run -- -g us -h en -y true -m smart

# Smart + trend — only rising suggestions vs the previous run
dotnet run -- -g us -h en -y true -m smart -t true
```
