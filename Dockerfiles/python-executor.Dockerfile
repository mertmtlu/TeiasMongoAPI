FROM python:3.12-slim

# Create non-root user for security
RUN useradd -m -u 1000 executor && \
    mkdir -p /app /output /tmp && \
    chown executor:executor /app /output /tmp

# Install common scientific packages (optional - adjust based on your needs)
# RUN pip install --no-cache-dir numpy pandas requests

WORKDIR /app

# Switch to non-root user
USER executor

# Default command
CMD ["python"]
