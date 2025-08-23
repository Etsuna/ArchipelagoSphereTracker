#!/bin/sh
set -eu

# --- Configuration -----------------------------------------------------------
: "${ENEMIZER_VERSION:=}"  # ex: v7.0.32, laisser vide pour ignorer
: "${APPIMAGETOOL_URL:=https://github.com/AppImage/appimagetool/releases/download/1.9.0/appimagetool-aarch64.AppImage}"

# On vise python3.12 si possible ; sinon on bascule sur python3
PY_BIN="python3.12"
: "${RELEASE_VERSION:=$(git describe --tags --abbrev=0 2>/dev/null || echo dev)}"
: "${INSTALL_PYGOBJECT_PIP:=0}"  # 0 = utiliser python3-gi système ; 1 = tenter pip (nécessite libcairo2-dev)

# --- Préconditions -----------------------------------------------------------
if [ "$(uname -s)" != "Linux" ]; then
  echo "Ce script est prévu pour Linux." >&2; exit 1
fi
if [ "$(uname -m)" != "aarch64" ]; then
  echo "Attention: arch détectée $(uname -m); ce script cible aarch64 (ARM64)." >&2
fi
if [ ! -f "setup.py" ]; then
  echo "Exécutez ce script depuis la racine du repo (setup.py introuvable)." >&2; exit 1
fi

# --- Nettoyage de tout PPA deadsnakes résiduel (plucky non supporté) --------
# (évite l'erreur 404 lors de apt-get update)
sudo rm -f /etc/apt/sources.list.d/deadsnakes-ubuntu-ppa-*.list /etc/apt/sources.list.d/deadsnakes-ubuntu-ppa-*.sources 2>/dev/null || true
sudo sed -i 's|^[^#].*deadsnakes.*|# &|g' /etc/apt/sources.list 2>/dev/null || true

# --- Dépendances système -----------------------------------------------------
echo "==> Installation des dépendances système"
sudo apt-get update
sudo apt-get install -y \
  build-essential p7zip xz-utils wget \
  libglib2.0-0 libgirepository1.0-dev gobject-introspection \
  python3-gi pkg-config \
  libcairo2 libcairo2-dev meson ninja-build

# --- Python (sans PPA) -------------------------------------------------------
if command -v "$PY_BIN" >/dev/null 2>&1; then
  :
else
  echo "==> $PY_BIN introuvable, tentative via dépôts Ubuntu"
  if sudo apt-get install -y python3.12 python3.12-venv python3.12-dev; then
    PY_BIN="python3.12"
  else
    echo "python3.12 indisponible. Bascule sur python3 du système."
    sudo apt-get install -y python3 python3-venv python3-dev
    PY_BIN="python3"
  fi
fi

# --- appimagetool (aarch64) --------------------------------------------------
echo "==> Récupération d'appimagetool (aarch64)"
rm -rf squashfs-root appimagetool appimagetool-aarch64.AppImage
wget -nv "$APPIMAGETOOL_URL" -O appimagetool-aarch64.AppImage
chmod a+rx appimagetool-aarch64.AppImage
./appimagetool-aarch64.AppImage --appimage-extract
# wrapper pour l'appeler simplement
printf '#/bin/sh\n./squashfs-root/AppRun "$@"\n' > appimagetool
chmod a+rx appimagetool

# --- Dépendances runtime (Enemizer, best-effort) -----------------------------
echo "==> (Optionnel) Téléchargement d'Enemizer ARM64"
mkdir -p EnemizerCLI
if [ -n "${ENEMIZER_VERSION}" ]; then
  ENEMIZER_FILE="ubuntu.16.04-arm64.7z"
  ENEMIZER_URL="https://github.com/Ijwu/Enemizer/releases/download/${ENEMIZER_VERSION}/${ENEMIZER_FILE}"
  if wget -nv "$ENEMIZER_URL"; then
    7za x -oEnemizerCLI/ "$ENEMIZER_FILE"
  else
    echo "Aucune build ARM64 d'Enemizer trouvée pour ${ENEMIZER_VERSION} — on continue sans."
  fi
else
  echo "ENEMIZER_VERSION non défini — on saute cette étape."
fi

# --- Build -------------------------------------------------------------------
echo "==> Build AppImage (ARM64)"
export ARCH=aarch64
export PYTHON="$PY_BIN"

# venv ; on inclut les paquets système pour réutiliser python3-gi installé via APT
"$PY_BIN" -m venv --system-site-packages venv
. venv/bin/activate

# Deps Python
python -m pip install --upgrade pip charset-normalizer

# PyGObject:
#  - Par défaut (INSTALL_PYGOBJECT_PIP=0), on N'installe PAS via pip => on utilise python3-gi du système
#  - Si INSTALL_PYGOBJECT_PIP=1, on tente via pip (libcairo2-dev requis ; on l'a installé plus haut)
if [ "$INSTALL_PYGOBJECT_PIP" = "1" ]; then
  python -m pip install "PyGObject<3.51.0"
fi

# Build cx_Freeze + AppImage (cibles définies dans setup.py)
python setup.py build_exe --yes bdist_appimage --yes

echo "==> Contenu build/:"
[ -d build ] && ls -la build || true
echo "==> Contenu dist/:"
[ -d dist ] && ls -la dist || true

# --- Post-traitement: nommage et tar.gz -------------------------------------
APPIMAGE_NAME=""
for f in dist/*.AppImage; do
  [ -e "$f" ] || continue
  APPIMAGE_NAME=$(basename "$f")
  break
done
[ -n "$APPIMAGE_NAME" ] || { echo "Erreur: aucune AppImage trouvée dans dist/." >&2; exit 1; }

TAR_NAME="${APPIMAGE_NAME%.AppImage}.tar.gz"

DIR_NAME=""
for d in build/exe*; do
  [ -d "$d" ] || continue
  DIR_NAME="$d"
  break
done
[ -n "$DIR_NAME" ] || { echo "Erreur: répertoire build/exe* introuvable." >&2; exit 1; }

(
  cd build
  base_dir=$(basename "$DIR_NAME")
  mv "$base_dir" Archipelago
  tar -cv Archipelago | gzip -8 > "../dist/$TAR_NAME"
  mv Archipelago "$base_dir"
)

echo "==> Terminé"
echo "AppImage : dist/$APPIMAGE_NAME"
echo "Archive  : dist/$TAR_NAME"
echo "Version  : $RELEASE_VERSION"
