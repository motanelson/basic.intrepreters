REM --- Number Guessing Game ---
PRINT "Welcome to Guess The Number!"
LET target = INT(RND(100)) + 1
LET tries = 0
GOTO ask_guess 
a100:
  PRINT "Congratulations! You found it in " + tries + " tries."
  END
ask_guess:
  INPUT "Enter guess (1-100): ", guess
  LET tries = tries + 1

  IF guess = target THEN GOTO PRINT "Congratulations! You found it in " + tries + " tries."
  IF guess = target THEN END    
  
  IF guess < target THEN PRINT "Higher!"
  IF guess > target THEN PRINT "Lower!"
  GOTO ask_guess
  

     
  END