# --- Script Configuration ---

# Set the template for data synthesis.
# Can be "instruction", "npc", or "knowledge".
$template = "vibes"

# Set the sample size. Set to 0 to use the full dataset.
$sample_size = 250

# Define the output path using an expandable string.
$out_path = "gemini_pro_${template}_synthesis_output.jsonl"

# --- Virtual Environment Activation ---

# Define the path to the PowerShell activation script within your virtual environment.
$venvActivationScript = ".\.venv\Scripts\Activate.ps1"

# Check if the activation script exists before trying to run it.
if (Test-Path $venvActivationScript) {
    # Execute the activation script.
    # The dot sourcing (.) at the beginning runs the script in the current scope,
    # which is necessary to modify the environment for subsequent commands in this script.
    . $venvActivationScript
    Write-Host "Virtual environment activated."
} else {
    # If the script isn't found, write an error and exit.
    Write-Error "Virtual environment activation script not found at '$venvActivationScript'"
    exit 1
}

# --- Python Script Execution ---

# Now that the venv is active, the 'python' command will automatically use
# the executable from ./venv/Scripts/python.exe.
# The PYTHONPATH adjustment might still be useful if you have local modules to import.
$env:PYTHONPATH = "."

Write-Host "Running Python synthesis script..."
python code/gemini_synthesize.py --template $template --sample_size $sample_size --output_path $out_path

# --- Deactivation (Optional) ---

# In a script, deactivation is often not necessary as the environment changes
# are limited to the script's execution. However, if you were in an interactive
# terminal, you could run the 'deactivate' function.
# deactivate
Write-Host "Script finished."