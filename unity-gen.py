#!/usr/bin/env python3

import os
import hashlib

def generate_guid(file_path):
    hash_object = hashlib.md5(file_path.encode('utf-8'))
    return hash_object.hexdigest().lower()

def create_meta_file(file_path, guid):
    meta_content = f"""fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    meta_file_path = f"{file_path}.meta"
    with open(meta_file_path, 'w') as meta_file:
        meta_file.write(meta_content)
    print(f"Generated .meta file: {meta_file_path}")

def process_directory(directory):
    for root, _, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs") and not ".target" in str(root):
                file_path = os.path.join(root, file)
                relative_path = os.path.relpath(file_path, start=directory)
                guid = generate_guid(relative_path)
                create_meta_file(file_path, guid)

def main():
    current_directory = os.getcwd()
    process_directory(current_directory)
    print("Done")

if __name__ == "__main__":
    main()
