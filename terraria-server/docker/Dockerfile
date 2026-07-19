FROM mcr.microsoft.com/dotnet/runtime:9.0

RUN apt-get update && apt-get install -y \
    bash jq wget unzip libgdiplus sqlite3 python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /tshock

RUN wget -q "https://github.com/Pryaxis/TShock/releases/download/v6.0.0/TShock-6.0.0-for-Terraria-1.4.5.5-linux-x64-Release.zip" -O tshock.zip && \
    unzip -q tshock.zip && \
    rm tshock.zip && \
    tar xf TShock-Beta-linux-x64-Release.tar && \
    rm TShock-Beta-linux-x64-Release.tar && \
    chmod +x TShock.Server

# Copy TShock DLLs for plugin compilation
RUN mkdir -p /tshock-ref && \
    cp /tshock/bin/TerrariaServer.dll /tshock-ref/ && \
    cp /tshock/bin/OTAPI.dll /tshock-ref/ && \
    cp /tshock/ServerPlugins/TShockAPI.dll /tshock-ref/

# Build ChatBridge plugin
COPY docker/chatbridge/ /tmp/chatbridge/
RUN dotnet publish /tmp/chatbridge/ChatBridge.csproj \
    -c Release \
    -o /tshock/ServerPlugins/ && \
    rm -rf /tmp/chatbridge /tshock-ref

COPY bootstrap.sh /tshock/bootstrap.sh
RUN chmod +x /tshock/bootstrap.sh

EXPOSE 7777 7878

ENTRYPOINT ["/bin/bash", "/tshock/bootstrap.sh"]
