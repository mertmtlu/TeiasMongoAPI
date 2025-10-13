FROM python:3.12-slim

# 1. Install system dependencies required by openseespy
#    - libquadmath0: Fortran runtime
#    - libblas3: Basic Linear Algebra Subprograms
#    - liblapack3: Linear Algebra Package (often needed with BLAS)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libquadmath0 \
        libblas3 \
        liblapack3 && \
    rm -rf /var/lib/apt/lists/*

# 2. Install openseespy globally in the container
RUN pip install --no-cache-dir openseespy

# 3. Create non-root user for security and create the app directory
RUN useradd -m -u 1000 executor && \
    mkdir -p /app /output /tmp && \
    chown -R executor:executor /app /output /tmp

# 4. Set the working directory
WORKDIR /app

# 5. Switch to the non-root user for running the application
USER executor

# 6. Set the default command to start an interactive Python session
CMD ["python"]