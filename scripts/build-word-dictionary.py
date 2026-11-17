"""Compatibility entry point for the deterministic shared dictionary builder."""
import argparse
from pathlib import Path
import subprocess
parser=argparse.ArgumentParser()
parser.add_argument('csv',type=Path)
args=parser.parse_args()
subprocess.run(['node',str(Path(__file__).with_name('build-word-details.cjs')),str(args.csv)],check=True)
