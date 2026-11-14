REM --- GOSUB / RETURN Demonstration ---
LET name = "Explorer"
GOSUB greet_user

LET score = 100
GOSUB print_score
END

greet_user:
  PRINT "Hello, " + name + "!"
  RETURN

print_score:
  PRINT "Current Score: " + score
  RETURN