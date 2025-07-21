#!/bin/bash
set -e  # Quitte en cas d'erreur

### Détection WSL
IS_WSL=false
if grep -qi microsoft /proc/sys/kernel/osrelease; then
  IS_WSL=true
  echo "⚠️ Environnement WSL détecté : certaines opérations seront ignorées (swap, systemd, cron…)."
fi

### 2) Swap 2Go (sauf si WSL)
if [ "$IS_WSL" = false ]; then
  if ! grep -q "/swapfile" /etc/fstab; then
    echo "💾 Ajout d’un swap 2Go"
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
    echo "ℹ️ Swap déjà configuré, rien à faire."
  fi
else
  echo "⏭️ Swap ignoré sous WSL"
fi

### 3) Dossier de travail
APPDIR="$HOME/ArchipelagoSphereTracker"
mkdir -p "$APPDIR"
cd "$APPDIR"

### 4) Télécharger la dernière release Linux x64
echo "🌐 Récupération de la dernière release…"
LATEST_URL=$(curl -s https://api.github.com/repos/Etsuna/ArchipelagoSphereTracker/releases/latest |
             grep "browser_download_url.*linux-x64.*\.tar\.gz" |
             cut -d '"' -f 4)
[ -z "$LATEST_URL" ] && { echo "❌ Release introuvable"; exit 1; }

FILENAME=$(basename "$LATEST_URL")
wget -q --show-progress "$LATEST_URL"
tar -xzf "$FILENAME" && rm "$FILENAME"
chmod +x ArchipelagoSphereTracker

### 5) Fichier .env
read -p "🔑 DISCORD_TOKEN: " DISCORD_TOKEN
read -p "🆔 APP_ID: "        APP_ID
cat > .env <<EOF
DISCORD_TOKEN=$DISCORD_TOKEN
APP_ID=$APP_ID
EOF
echo "✅ .env créé"

### 6) Installation interne
./ArchipelagoSphereTracker install

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
  echo "🚀 Service systemd actif! (sudo systemctl status archipelagospheretracker)"
else
  echo "⚠️ systemd non pris en charge sous WSL. Service non installé."
fi

### 8) Script update_and_restart.sh
cat > update_and_restart.sh <<'UPD'
#!/bin/bash
# Arrêter le service
sudo systemctl stop archipelagospheretracker.service
# S’assurer que le binaire est exécutable
chmod +x "$(dirname "$0")/ArchipelagoSphereTracker"
# Redémarrer le service
sudo systemctl start archipelagospheretracker.service
echo "✅ Mise à jour et redémarrage terminés."
UPD
chmod +x update_and_restart.sh
echo "🛠  Script update_and_restart.sh créé"

### 9) Tâche cron : reboot quotidien (si pas WSL)
if [ "$IS_WSL" = false ]; then
  if ! sudo crontab -l 2>/dev/null | grep -q "/usr/sbin/reboot"; then
    (sudo crontab -l 2>/dev/null; echo "0 0 * * * /usr/sbin/reboot") | sudo crontab -
    echo "⏰ Cron: reboot quotidien ajouté"
  else
    echo "⏰ Cron reboot déjà présent"
  fi
  sudo systemctl enable --now cron
else
  echo "⏭️ Cron ignoré sous WSL"
fi

echo "✅ Installation complète !"
