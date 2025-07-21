#!/bin/bash
set -e

### Détection WSL
IS_WSL=false
if grep -qi microsoft /proc/sys/kernel/osrelease; then
  IS_WSL=true
  echo "⚠️ Environnement WSL détecté"
fi

echo "🔧 Désinstallation de ArchipelagoSphereTracker..."

### 1) Stop & disable systemd service (si pas WSL)
SERVICE_NAME="archipelagospheretracker"
SERVICE_PATH="/etc/systemd/system/${SERVICE_NAME}.service"

if [ "$IS_WSL" = false ]; then
  if systemctl is-enabled --quiet "$SERVICE_NAME"; then
    echo "⛔️ Arrêt et désactivation du service $SERVICE_NAME"
    sudo systemctl stop "$SERVICE_NAME"
    sudo systemctl disable "$SERVICE_NAME"
  fi

  if [ -f "$SERVICE_PATH" ]; then
    echo "🗑 Suppression du fichier de service systemd"
    sudo rm "$SERVICE_PATH"
    sudo systemctl daemon-reload
  fi
else
  echo "⏭️ systemd ignoré sous WSL"
fi

### 2) Supprimer le dossier de l’application
APPDIR="$HOME/ArchipelagoSphereTracker"
if [ -d "$APPDIR" ]; then
  echo "🧹 Suppression du dossier $APPDIR"
  rm -rf "$APPDIR"
else
  echo "ℹ️ Aucun dossier $APPDIR à supprimer."
fi

### 3) Nettoyage de la tâche cron de reboot (si pas WSL)
if [ "$IS_WSL" = false ]; then
  echo "🗑 Suppression de la tâche cron de reboot automatique (si présente)"
  CURRENT_CRON=$(sudo crontab -l 2>/dev/null || true)
  UPDATED_CRON=$(echo "$CURRENT_CRON" | grep -vE '^0 0 \* \* \* /usr/sbin/reboot$')

  if [ "$CURRENT_CRON" != "$UPDATED_CRON" ]; then
    if [ -z "$UPDATED_CRON" ]; then
      sudo crontab -r
      echo "✅ Cron supprimé entièrement (aucune autre tâche)."
    else
      echo "$UPDATED_CRON" | sudo crontab -
      echo "✅ Cron mise à jour (autres tâches conservées)."
    fi
  else
    echo "ℹ️ Aucune tâche cron de reboot trouvée."
  fi
else
  echo "⏭️ Cron ignoré sous WSL"
fi

### 4) Suppression de la swap (si pas WSL)
if [ "$IS_WSL" = false ]; then
  if swapon --show | grep -q "/swapfile"; then
    echo "↪️ Désactivation du swap..."
    sudo swapoff /swapfile
  fi

  if [ -f /swapfile ]; then
    echo "🗑 Suppression de /swapfile"
    sudo rm /swapfile
  fi

  echo "✏️ Nettoyage de /etc/fstab (suppression ligne /swapfile)"
  sudo sed -i.bak '/\/swapfile/d' /etc/fstab
else
  echo "⏭️ Swap ignoré sous WSL"
fi

### 5) Vérification finale
echo "🧾 État final de la crontab :"
sudo crontab -l || echo "📭 Crontab vide"

echo "💾 État du swap :"
swapon --show || echo "✔️ Aucun swap actif"

echo "🛑 État du service $SERVICE_NAME :"
if [ "$IS_WSL" = false ]; then
  systemctl status "$SERVICE_NAME" --no-pager || echo "✅ Service supprimé"
else
  echo "⏭️ Ignoré sous WSL"
fi

echo "✅ Désinstallation terminée avec succès."
