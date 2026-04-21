#!/bin/bash

# Builds the mod, cleans the deployed mod folder, and re-deploys,
# ensuring stale Defs/Patches/Assemblies are removed.
#
# Usage:
#   ./Scripts/clean-build.sh

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_ROOT="$(dirname "$SCRIPT_DIR")"
MOD_NAME="ArchotechAndroidHardware"

# Resolve RimWorld path
if [ -z "$RIMWORLD_PATH" ]; then
    RIMWORLD_PATH="/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld"
fi
DEPLOY_PATH="$RIMWORLD_PATH/Mods/$MOD_NAME"

echo -e "${BLUE}=== Archotech Android Hardware - Clean Build ===${NC}"
echo -e "Source: $MOD_ROOT"
echo -e "Target: $DEPLOY_PATH"
echo ""

# Validate RimWorld path
if [ ! -d "$RIMWORLD_PATH" ]; then
    echo -e "${RED}✗ RimWorld not found at: $RIMWORLD_PATH${NC}"
    echo -e "${RED}  Set RIMWORLD_PATH in ~/.bashrc${NC}"
    exit 1
fi

# Step 1: Build
echo -e "${YELLOW}[1/3] Building...${NC}"
cd "$MOD_ROOT"
dotnet build ArchotechAndroidHardware.sln -c Release --verbosity quiet

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Build succeeded${NC}"
echo ""

# Step 2: Clean deployed mod folder
echo -e "${YELLOW}[2/3] Cleaning mod folder...${NC}"
if [ -d "$DEPLOY_PATH" ]; then
    rm -rf "$DEPLOY_PATH"
    echo -e "${GREEN}✓ Mod folder cleaned${NC}"
else
    echo -e "${GREEN}✓ No existing mod folder to clean${NC}"
fi
echo ""

# Step 3: Deploy fresh
echo -e "${YELLOW}[3/3] Deploying...${NC}"
mkdir -p "$DEPLOY_PATH/1.6/Assemblies"
mkdir -p "$DEPLOY_PATH/1.6/Defs"

cp -r "$MOD_ROOT/About" "$DEPLOY_PATH/"
cp "$MOD_ROOT/LoadFolders.xml" "$DEPLOY_PATH/"
cp "$MOD_ROOT/1.6/Assemblies/"*.dll "$DEPLOY_PATH/1.6/Assemblies/" 2>/dev/null
cp -r "$MOD_ROOT/1.6/Defs/"* "$DEPLOY_PATH/1.6/Defs/"
if [ -d "$MOD_ROOT/1.6/Patches" ]; then
    mkdir -p "$DEPLOY_PATH/1.6/Patches"
    cp -r "$MOD_ROOT/1.6/Patches/"* "$DEPLOY_PATH/1.6/Patches/"
fi
if [ -d "$MOD_ROOT/Textures" ]; then
    cp -r "$MOD_ROOT/Textures" "$DEPLOY_PATH/"
fi

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Deploy failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Deploy succeeded${NC}"
echo ""

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Clean build complete${NC}"
echo -e "${GREEN}========================================${NC}"
