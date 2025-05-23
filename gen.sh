#!/bin/bash
# Generates SERVERS.md from servers.json

set -e

JSON_FILE="servers.json"
MD_FILE="SERVERS.md"

echo "# BadgeFed Servers" > "$MD_FILE"
echo "| Name | Description | Categories | Follow |" >> "$MD_FILE"
echo "|------|-------------|------------|--------|" >> "$MD_FILE"

jq -r '.[] | "| [" + (.name // "") + "](" + (.url // "") + ") | " + (.description // "") + " | " + ((.categories // []) | join(", ")) + " | [Follow](" + (.admin // "") + ") |"' "$JSON_FILE" >> "$MD_FILE"

echo "Generated $MD_FILE from $JSON_FILE."
