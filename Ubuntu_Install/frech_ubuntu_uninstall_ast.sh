#!/bin/bash
set -e

### Détection WSL
IS_WSL=false
if grep -qi microsoft /proc/sys/kernel/osrelease; then
  IS_WSL=true
  echo "⚠️ WSL environment detected"
fi

echo "🔧 Uninstalling ArchipelagoSphereTracker..."

### 1) Stop & disable systemd service (si pas WSL)
SERVICE_NAME="archipelagospheretracker"
SERVICE_PATH="/etc/systemd/system/${SERVICE_NAME}.service"

if [ "$IS_WSL" = false ]; then
  if systemctl is-enabled --quiet "$SERVICE_NAME"; then
    echo "⛔️ Stopping and disabling service $SERVICE_NAME"
    sudo systemctl stop "$SERVICE_NAME"
    sudo systemctl disable "$SERVICE_NAME"
  fi

  if [ -f "$SERVICE_PATH" ]; then
    echo "🗑 Removing systemd service file"
    sudo rm "$SERVICE_PATH"
    sudo systemctl daemon-reload
  fi
else
  echo "⏭️ systemd ignored under WSL"
fi

### 2) Supprimer le dossier de l’application
APPDIR="$HOME/ArchipelagoSphereTracker"
if [ -d "$APPDIR" ]; then
  echo "🧹 Removing folder $APPDIR"
  rm -rf "$APPDIR"
else
  echo "ℹ️ No folder $APPDIR to remove."
fi

### 3) Nettoyage de la tâche cron de reboot (si pas WSL)
if [ "$IS_WSL" = false ]; then
  echo "🗑 Removing automatic reboot cron job (if present)"
  CURRENT_CRON=$(sudo crontab -l 2>/dev/null || true)
  UPDATED_CRON=$(echo "$CURRENT_CRON" | grep -vE '^0 0 \* \* \* /usr/sbin/reboot$')

  if [ "$CURRENT_CRON" != "$UPDATED_CRON" ]; then
    if [ -z "$UPDATED_CRON" ]; then
      sudo crontab -r
      echo "✅ Cron fully removed (no other jobs)."
    else
      echo "$UPDATED_CRON" | sudo crontab -
      echo "✅ Cron updated (other jobs preserved)."
    fi
  else
    echo "ℹ️ No reboot cron job found."
  fi
else
  echo "⏭️ Cron ignored under WSL"
fi

### 4) Suppression de la swap (si pas WSL)
if [ "$IS_WSL" = false ]; then
  if swapon --show | grep -q "/swapfile"; then
    echo "↪️ Disabling swap..."
    sudo swapoff /swapfile
  fi

  if [ -f /swapfile ]; then
    echo "🗑 Removing /swapfile"
    sudo rm /swapfile
  fi

  echo "✏️ Cleaning /etc/fstab (removing /swapfile line)"
  sudo sed -i.bak '/\/swapfile/d' /etc/fstab
else
  echo "⏭️ Swap ignored under WSL"
fi

### 5) Vérification finale
echo "🧾 Final crontab state:"
sudo crontab -l || echo "📭 Empty crontab"

echo "💾 Swap status:"
swapon --show || echo "✔️ No active swap"

echo "🛑 Service status $SERVICE_NAME:"
if [ "$IS_WSL" = false ]; then
  systemctl status "$SERVICE_NAME" --no-pager || echo "✅ Service removed"
else
  echo "⏭️ Ignored under WSL"
fi

echo "✅ Uninstallation completed successfully."
