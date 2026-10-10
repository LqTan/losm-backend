FROM python:3.12-slim

RUN pip install --no-cache-dir overturemaps duckdb

WORKDIR /work

ENTRYPOINT ["overturemaps"]
CMD ["--help"]