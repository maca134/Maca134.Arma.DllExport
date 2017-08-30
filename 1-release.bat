set /p KeyPassword=Enter key password: 
nant "-D:keypassword=%KeyPassword%"
pause