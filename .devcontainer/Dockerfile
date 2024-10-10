# Use the official .NET 8 SDK image as a base
FROM mcr.microsoft.com/dotnet/sdk:8.0

# Install dependencies
RUN apt-get update && apt-get install -y wget apt-transport-https

# Add Microsoft package repository and install Azure Functions Core Tools
RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg \
    && wget -qO- https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/prod.list > /etc/apt/sources.list.d/microsoft-prod.list \
    && apt-get update \
    && apt-get install -y azure-functions-core-tools-4

# Set the working directory
WORKDIR /workspace


# Copy the project files
COPY . .

# Restore the project dependencies
RUN dotnet restore