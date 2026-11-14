REM --- Fibonacci Generator ---
INPUT "How many terms? ", n
LET a = 0
LET b = 1
LET i = 0

PRINT "Fibonacci sequence:"

loop:
  IF i >= n THEN END
  PRINT a
  LET temp = a + b
  LET a = b
  LET b = temp
  LET i = i + 1
  GOTO loop

finished:
  PRINT "Done!"
  END