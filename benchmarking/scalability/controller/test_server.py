import pytest
from pathlib import Path

from server import app


@pytest.fixture
def client():
    with app.test_client() as c:
        yield c


def test_post_valid_json(client):
    payload = {"a": 1, "b": "x"}
    rv = client.post('/json', json=payload)
    assert rv.status_code == 200
    data = rv.get_json()
    assert data["status"] == "ok"
    assert data["received"] == payload


def test_post_invalid_json(client):
    # Send plain text — not JSON
    rv = client.post('/json', data="not json", content_type="text/plain")
    assert rv.status_code == 400


def test_post_non_object_json(client):
    # Send a JSON array — endpoint expects an object/dict
    rv = client.post('/json', json=[1, 2, 3])
    assert rv.status_code == 400


def test_upload_creates_directory_and_saves_file(tmp_path, client):
    # create a temporary file to upload
    p = tmp_path / 'hello.txt'
    p.write_text('hello')

    with open(p, 'rb') as f:
        multipart = {'file': (f, 'hello.txt')}
        rv = client.post('/upload', data={**multipart, 'dir': 'nested/dir'}, content_type='multipart/form-data')

    assert rv.status_code == 200
    resp = rv.get_json()
    assert resp['status'] == 'ok'
    # check that the file exists on disk relative to the controller folder
    saved = resp['saved_path']
    saved_path = Path(__file__).parent.joinpath(saved)
    assert saved_path.exists()


def test_upload_rejects_traversal(client, tmp_path):
    # attempt to use traversal
    with open(tmp_path.joinpath('f.txt'), 'wb') as fh:
        fh.write(b'hi')
    with open(tmp_path.joinpath('f.txt'), 'rb') as f:
        multipart = {'file': (f, 'f.txt')}
        rv = client.post('/upload', data=multipart | {'dir': '../outside'}, content_type='multipart/form-data')
    assert rv.status_code == 400


def test_subscribe_client_valid(client):
    payload = {"nodeID": "node-1", "addresses": ["10.0.0.1", "10.0.0.2"]}
    rv = client.post('/subscribe_client', json=payload)
    assert rv.status_code == 200
    data = rv.get_json()
    assert data['status'] == 'ok'
    assert data['nodeID'] == payload['nodeID']
    assert data['addresses'] == payload['addresses']


def test_subscribe_client_invalid_missing_fields(client):
    # missing nodeID
    payload = {"addresses": ["10.0.0.1"]}
    rv = client.post('/subscribe_client', json=payload)
    assert rv.status_code == 400

