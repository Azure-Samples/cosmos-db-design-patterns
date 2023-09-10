FROM mcr.microsoft.com/devcontainers/dotnet:6.0-bullseye

# Install Azure Functions Core Tools
RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg

RUN sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg

RUN sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/debian/$(lsb_release -rs | cut -d'.' -f 1)/prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list'

RUN sudo apt-get update

RUN sudo apt-get install azure-functions-core-tools-4