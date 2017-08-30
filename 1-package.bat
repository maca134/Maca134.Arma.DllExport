set /p KeyPassword=Enter key password: 
nant package "-D:keypassword=%KeyPassword%"
pause