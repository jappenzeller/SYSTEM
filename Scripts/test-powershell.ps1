param(
    [string]$Message = "Hello World"
)

Write-Host "Test script running..."
Write-Host "Message: $Message"

function Test-Function {
    Write-Host "Function called successfully"
}

Test-Function
Write-Host "Script completed"
exit 0