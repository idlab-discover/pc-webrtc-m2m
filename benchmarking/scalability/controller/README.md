Flask JSON receiver

Endpoints:
- GET / -> health check
- POST /json -> accepts a JSON object and echoes it back

Run locally:

1. Create a virtualenv and install requirements:
   python -m venv .venv; .\.venv\Scripts\Activate.ps1; pip install -r requirements.txt

2. Start the server:
   python server.py

Run tests:
   pytest -q
