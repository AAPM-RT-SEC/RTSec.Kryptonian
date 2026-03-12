FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm

# The base image may include a Yarn apt source with an unavailable key.
# Remove it so apt-get update can proceed reliably.
RUN rm -f /etc/apt/sources.list.d/yarn.list /etc/apt/sources.list.d/yarn*.list \
	&& apt-get update \
	&& apt-get install -y --no-install-recommends \
		openssl \
		postgresql-client \
		ca-certificates \
	&& rm -rf /var/lib/apt/lists/*
