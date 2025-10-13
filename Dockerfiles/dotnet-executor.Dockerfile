FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine

# 1. Install any additional system dependencies if needed
#    Currently using Alpine base which includes essentials
#    Add packages here if specific runtime libraries are required
# RUN apk add --no-cache <package-name>

# 2. Create non-root user for security and create the app directory
#    - Creates user 'executor' with UID 1000 for consistency
#    - Sets up /app (project), /output (results), /tmp (temporary files)
#    - Grants ownership to executor user for all directories
RUN adduser -D -u 1000 executor && \
    mkdir -p /app /output /tmp && \
    chown -R executor:executor /app /output /tmp

# 3. Set the working directory
WORKDIR /app

# 4. Switch to the non-root user for running the application
USER executor

# 5. Set the default command to dotnet CLI
CMD ["dotnet"]
