FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime-base

RUN apt-get update && apt-get install -y \
    bash jq wget unzip libgdiplus sqlite3 python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tshock

RUN wget -q "https://github.com/Pryaxis/TShock/releases/download/v6.1.0/TShock-6.1.0-for-Terraria-1.4.5.6-linux-x64-Release.zip" -O tshock.zip && \
    unzip -q tshock.zip && \
    rm tshock.zip && \
    tar xf *.tar && \
    rm -f *.tar && \
    ls -la && \
    chmod +x TShock.Server

# Copy TShock DLLs for plugin compilation
RUN mkdir -p /tshock-ref && \
    cp /tshock/bin/TerrariaServer.dll /tshock-ref/ && \
    cp /tshock/bin/OTAPI.dll /tshock-ref/ && \
    cp /tshock/ServerPlugins/TShockAPI.dll /tshock-ref/

# Build ChatBridge plugin using SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS plugin-build
WORKDIR /build
COPY chatbridge/ .
COPY --from=runtime-base /tshock-ref/ ./tshock-ref/
RUN dotnet publish ChatBridge.csproj -c Release -o /output

# Final image: runtime + plugin
FROM runtime-base
COPY --from=plugin-build /output/ChatBridgePlugin.dll /tshock/ServerPlugins/
RUN rm -rf /tshock-ref

COPY bootstrap.sh /tshock/bootstrap.sh
RUN chmod +x /tshock/bootstrap.sh

EXPOSE 7777 7878

ENTRYPOINT ["/bin/bash", "/tshock/bootstrap.sh"]
