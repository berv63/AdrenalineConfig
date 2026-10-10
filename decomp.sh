#!/bin/bash
# Decompiles the game types this mod patches into ./decomp for reference. Runs in the SDK container
# with the game folder mounted at /game and this folder at /src:
#   docker run --rm -v "<game>:/game:ro" -v "<this folder>:/src" mcr.microsoft.com/dotnet/sdk:8.0 bash -c "tr -d '\r' < /src/decomp.sh | bash"
dotnet tool install -g ilspycmd --version 8.2.0.7535 >/dev/null 2>&1
export PATH=$PATH:/root/.dotnet/tools
export DOTNET_ROLL_FORWARD=LatestMajor
for t in Character Attack SEMan StatusEffect SE_Stats Hud Player Humanoid Game ItemDrop Projectile; do
    ilspycmd -t $t /game/valheim_Data/Managed/assembly_valheim.dll > /src/decomp/$t.cs 2> /src/decomp/$t.err || echo "FAIL $t"
    [ -s /src/decomp/$t.err ] || rm -f /src/decomp/$t.err
done
ls -la /src/decomp
