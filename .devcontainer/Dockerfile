FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

RUN apt-get update

RUN apt-get install --yes curl gnupg lsb-release apt-utils

RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg

RUN mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg

RUN sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/debian/$(lsb_release -rs 2>/dev/null | cut -d'.' -f 1)/prod $(lsb_release -cs 2>/dev/null) main" > /etc/apt/sources.list.d/dotnetdev.list'

# Add Microsoft package repository and install Azure Functions Core Tools
RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg \
    && wget -qO- https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/prod.list > /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update \
    && apt-get install -y azure-functions-core-tools-4

RUN apt-get update

RUN apt-get install --yes azure-functions-core-tools-4
