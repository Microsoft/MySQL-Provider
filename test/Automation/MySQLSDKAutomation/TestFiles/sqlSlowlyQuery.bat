@Echo Off
Setlocal EnableDelayedExpansion
Echo Usage: 
Echo demo: sql.bat HostName TotalRepeatCount
Echo eg. sql.bat m50p-ora5-02.scx.com 2
SET _TotalCount=%2
:_Repeat
If %_TotalCount% equ 0 GOTO :_End
Set /a _TotalCount-=1
SET _Str=
SET _Len=
SET _count=
Set _RNDLength=8
Set _Alphanumeric=ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789
Set _Str=%_Alphanumeric%987654321
:_LenLoop
IF NOT "%_Str:~18%"=="" SET _Str=%_Str:~9%& SET /A _Len+=9& GOTO :_LenLoop
SET _tmp=%_Str:~9,1%
SET /A _Len=_Len+_tmp
Set _count=0
SET _RndAlphaNum=
:_loop
Set /a _count+=1
SET _RND=%Random%
Set /A _RND=_RND%%%_Len%
SET _RndAlphaNum=!_RndAlphaNum!!_Alphanumeric:~%_RND%,1!
If !_count! lss %_RNDLength% goto _loop
Echo Random string is !_RndAlphaNum!
.\mysqlslap.exe --user=root --host=%1  --concurrency=10 --iterations=1000 --create-schema=employees --query="select sleep(2.0003);"
GOTO :_Repeat
:_End