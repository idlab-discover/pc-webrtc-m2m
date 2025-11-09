import json
import subprocess
import time
base_paths = ["json_example.json", "json_example_multi.json"]
session_mgr_paths = [
    [
        "/users/madfruge/pc-webrtc-m2m-cpy/benchmarking/controller/config/manager_simple_remote_random.json",
        "/users/madfruge/pc-webrtc-m2m-cpy/benchmarking/controller/config/manager_simple_remote_mdc.json"
    ],
    [
        "/users/madfruge/pc-webrtc-m2m-cpy/benchmarking/controller/config/manager_simple_remote_random_2sfu.json",
        "/users/madfruge/pc-webrtc-m2m-cpy/benchmarking/controller/config/manager_simple_remote_mdc_2sfu.json"
    ]
]
session_mgr_names = ["random", "mdc"]

base_names = ["sfu1", "sfu2"]
#n_clients = [
#    [1, 0, 1, 0, 1, 0, 1, 0], # 4 clients
#    [1, 1, 1, 1, 1, 1, 1, 1], # 8 clients
#    [2, 1, 2, 1, 2, 1, 2, 1], # 12 clients
#    [2, 2, 2, 2, 2, 2, 2, 2], # 16 clients
#    [3, 2, 3, 2, 3, 2, 3, 2], # 20 clients
#    [3, 3, 3, 3, 3, 3, 3, 3], # 24 clients
#    [4, 3, 4, 3, 4, 3, 4, 3], # 28 clients
#    [4, 4, 4, 4, 4, 4, 4, 4]  # 32 clients
#]
#n_no_send_clients = [
#    [0, 0, 0, 0, 0, 0, 0, 0], # 4 clients
#    [0, 1, 0, 1, 0, 1, 0, 1], # 8 clients
#    [1, 1, 1, 1, 1, 1, 1, 1], # 12 clients
#    [1, 2, 1, 2, 1, 2, 1, 2], # 16 clients
#    [2, 2, 2, 2, 2, 2, 2, 2], # 20 clients
#    [2, 3, 2, 3, 2, 3, 2, 3], # 24 clients
#    [3, 3, 3, 3, 3, 3, 3, 3], # 28 clients
#    [3, 4, 3, 4, 3, 4, 3, 4]  # 32 clients
#]
n_clients = [
    [4, 4, 4, 4, 4, 4, 4, 4]  # 32 clients
]
n_no_send_clients = [
    [3, 4, 3, 4, 3, 4, 3, 4]  # 32 clients
]
n_iterations = 1
exp_duration = 360  # in seconds
extra_sleep = 30
for i in range(len(base_paths)):
    if i == 0:
        continue
    base_name = base_names[i]
    base_path = base_paths[i]
    data = None
    with open(base_path, "r") as f:
        data = json.load(f)
    data["experimentDurationSeconds"] = exp_duration
    for j in range(len(session_mgr_paths[i])):
        session_mgr_path = session_mgr_paths[i][j]
        session_mgr_name = session_mgr_names[j]
        data["sessionManagerConfig"]["configPath"] = session_mgr_path
        for v in range(len(n_clients)):
            n_client = n_clients[v]
            counter = 0
            for client in data["clients"]:
                num = int(client["nodeID"][len("client"):])
                client["nClients"] = n_client[num-1]
                if j == 1:
                    client["disableSendingForNClients"] = n_no_send_clients[v][num-1]
                counter += n_client[num-1]
            data['logsSubDirectory'] = f"{base_name}_{session_mgr_name}_cl{counter}"
            with open(f"{base_name}_{session_mgr_name}_{counter}.json", "w") as f:
                json.dump(data, f, indent=4)  
            for k in range(n_iterations):
                print(f"Running experiment {base_name} using {session_mgr_name} with {counter} clients, iteration {k+1}")
                subprocess.run(["python", "runner.py", "--file", 
                f"{base_name}_{session_mgr_name}_{counter}.json", "--send-config", "--live"
                ])
                time.sleep(exp_duration + extra_sleep)

