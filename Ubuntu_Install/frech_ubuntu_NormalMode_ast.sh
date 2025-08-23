#!/bin/bash
set -e  # Quitte en cas d'erreur

### Détection WSL
IS_WSL=false
if grep -qi microsoft /proc/sys/kernel/osrelease; then
  IS_WSL=true
  echo "⚠️ WSL environment detected: some operations will be skipped (swap, systemd, cron…)."
fi

### 2) Swap 2Go (sauf si WSL)
if [ "$IS_WSL" = false ]; then
  if ! grep -q "/swapfile" /etc/fstab; then
    echo "💾 Added a 2GB swap"
    if [ ! -f /swapfile ]; then
      sudo fallocate -l 2G /swapfile
      sudo chmod 600 /swapfile
      sudo mkswap /swapfile
    fi

    if ! swapon --show | grep -q "/swapfile"; then
      sudo swapon /swapfile
    fi

    echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
  else
    echo "ℹ️ Swap already configured, nothing to do."
  fi
else
  echo "⏭️ Swap ignored under WSL"
fi

### 3) Dossier de travail
APPDIR="$HOME/ArchipelagoSphereTracker"
mkdir -p "$APPDIR"
cd "$APPDIR"

### 4) Télécharger la dernière release Linux (x64 ou arm64)
echo "🌐 Fetching the latest release…"

ARCH=""
case "$(uname -m)" in
  x86_64) ARCH="x64" ;;
  aarch64) ARCH="arm64" ;;
  *) echo "❌ Unsupported architecture: $(uname -m)"; exit 1 ;;
esac
echo "🔎 Detected architecture: $ARCH"

LATEST_URL=$(curl -s https://api.github.com/repos/Etsuna/ArchipelagoSphereTracker/releases/latest |
             grep "browser_download_url.*linux-$ARCH.*\.tar\.gz" |
             cut -d '"' -f 4)

[ -z "$LATEST_URL" ] && { echo "❌ Release not found for linux-$ARCH"; exit 1; }

FILENAME=$(basename "$LATEST_URL")
wget -q --show-progress "$LATEST_URL"
tar -xzf "$FILENAME" && rm "$FILENAME"
chmod +x ArchipelagoSphereTracker

### 6) Installation interne
./ArchipelagoSphereTracker --NormalMode

### 7) Service systemd (si pas WSL)
if [ "$IS_WSL" = false ]; then
  SERVICE_PATH=/etc/systemd/system/archipelagospheretracker.service
  sudo tee "$SERVICE_PATH" > /dev/null <<SERVICE
[Unit]
Description=Archipelago Sphere Tracker
After=network.target

[Service]
WorkingDirectory=$APPDIR
ExecStart=$APPDIR/ArchipelagoSphereTracker
Restart=always
User=$USER

[Install]
WantedBy=multi-user.target
SERVICE

  sudo systemctl daemon-reload
  sudo systemctl enable --now archipelagospheretracker.service
  echo "🚀 systemd service active! (sudo systemctl status archipelagospheretracker)"
else
  echo "⚠️ systemd not supported under WSL. Service not installed."
fi

### 8) Script update_and_restart.sh
cat > update_and_restart.sh <<'UPD'
#!/bin/bash
# Stop the service
sudo systemctl stop archipelagospheretracker.service
# Ensure the binary is executable
chmod +x "$(dirname "$0")/ArchipelagoSphereTracker"
# Restart the service
sudo systemctl start archipelagospheretracker.service
echo "✅ Update and restart completed."
UPD
chmod +x update_and_restart.sh
echo "🛠 update_and_restart.sh script created"

### 9) Tâche cron : reboot quotidien (si pas WSL)
if [ "$IS_WSL" = false ]; then
  if ! sudo crontab -l 2>/dev/null | grep -q "/usr/sbin/reboot"; then
    (sudo crontab -l 2>/dev/null; echo "0 0 * * * /usr/sbin/reboot") | sudo crontab -
    echo "⏰ Cron: daily reboot added"
  else
    echo "⏰ Cron reboot already present"
  fi
  sudo systemctl enable --now cron
else
  echo "⏭️ Cron ignored under WSL"
fi

echo "✅ Installation complete!"
