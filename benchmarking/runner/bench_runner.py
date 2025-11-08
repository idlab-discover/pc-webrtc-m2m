import json
import subprocess
base_paths = ["json_example.json"]
base_names = ["sfu1"]
n_clients = [
    [1, 0, 1, 0, 1, 0, 1, 0], 
    [1, 1, 1, 1, 1, 1, 1, 1]
]
exp_duration = 30  # in seconds
extra_sleep = 30
for i in range(len(base_paths)):
    base_name = base_names[i]
    base_path = base_paths[i]
    data = None
    with open(base_path, "r") as f:
        data = json.load(f)
    data['logsSubDirectory'] = base_name
    data["experimentDurationSeconds"] = exp_duration
    for n_client in n_clients:
        counter = 0
        for client in data["clients"]:
            num = int(client["nodeID"][len("client"):])
            client["nClients"] = n_client[num-1]
            counter += n_client[num-1]
        with open(f"{base_name}_{counter}.json", "w") as f:
            json.dump(data, f, indent=4)  
            
        subprocess.run(["python", "runner.py", "--file", 
        f"{base_name}_{counter}.json", "--send-config", "--live"
        ])
