@echo off
rem Runs the balance simulator with the hairdeck venv python (CUDA). Extra args pass through, e.g. run.cmd --tag test --out C:\temp\sim
"C:\AI\hairdeck\.venv\Scripts\python.exe" "%~dp0run_sim.py" %*
