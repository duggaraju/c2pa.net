import re
import os
import subprocess

data = []

project_root = f"{os.path.dirname(os.path.realpath(__file__))}\\.."

with open(f"{project_root}\\Directory.Build.props", "r") as f:
    for line in f.readlines():
        if "<PackageVersion>" in line:
            match = re.search(r"[0-9]*\.[0-9]*\.[0-9]*\.[0-9]*", line)
            old_version = match.group()
            new_version_arr = old_version.split(".")
            new_version_arr[-1] = str(int(new_version_arr[-1]) + 1)
            
            #new_version_str_arr = [str(x) for x in new_version_arr]
            line = line.replace(old_version, ".".join(new_version_arr))
        
        data.append(line)


with open(f"{project_root}\\Directory.Build.props", "w") as f:
    for line in data:
        f.write(line)

try:
    subprocess.run(["dotnet", "pack"], cwd = project_root)
except subprocess.CalledProcessError as e:
    print("Error:", e.stderr)