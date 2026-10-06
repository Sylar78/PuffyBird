#!/usr/bin/env bash
# Libère ~30 Go sur un runner Ubuntu GitHub pour l'image Docker de Unity (≈ 15 Go).
# Remplace jlumbroso/free-disk-space, qui désinstallait les paquets apt un par un (≈ 6 min) :
# ici les dossiers sont supprimés en parallèle (≈ 1 min). Rien de tout cela ne sert au build,
# qui tourne entièrement dans le conteneur GameCI.
set -u
df -h /
dirs=(
  /usr/local/lib/android          # SDK Android du runner (GameCI a le sien)
  /usr/share/dotnet
  /opt/ghc /usr/local/.ghcup      # Haskell
  /opt/hostedtoolcache            # Python, Node, Go… préinstallés
  /usr/local/share/powershell
  /usr/share/swift
  /usr/local/lib/node_modules
  /usr/local/share/chromium /opt/google/chrome /usr/lib/firefox
  /opt/microsoft /opt/az /usr/lib/google-cloud-sdk
  /usr/lib/jvm
  /usr/share/miniconda
  /usr/local/julia*
)
for d in "${dirs[@]}"; do
  sudo rm -rf $d &
done
# Images Docker préchargées sur le runner (inutiles : seule l'image Unity est tirée).
sudo docker image prune --all --force > /dev/null &
wait
df -h /
