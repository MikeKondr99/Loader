parser grammar LangParser;
options { tokenVocab=LangLexer; }

start:
    (constDecl | letDecl)* expr EOF;

constDecl
    : CONST NAME EQUAL expr SEMICOLON;

letDecl
    : LET NAME EQUAL expr SEMICOLON;

expr
    : MINUS expr #unary
    | <assoc=right> expr (HAT) expr #binary
    | expr (MUL | DIV) expr #binary
    | expr (PLUS | MINUS) expr #binary
    | expr (LESS_THEN | LESS_EQUAL | GREATER_THEN | GREATER_EQUAL) expr #binary
    | expr (EQUAL | NOT_EQUAL) expr #binary
    | expr AND expr #binary
    | expr OR expr #binary
    | term #term_expr
    ;

term
    : LEFT_PARENTHESIS expr RIGHT_PARENTHESIS #scope
    | string #literal
    | boolean #literal
    | null #literal
    | name #literal
    | func #function
    | integer #literal
    | number #literal
    | term DOT NAME LEFT_PARENTHESIS (expr (COMMA expr)*)? RIGHT_PARENTHESIS #objectFunction;

string: QUOTE stringContents* QUOTE;
stringContents: TEXT | (CURLY_OPEN expr CURLY_CLOSE) | ESCAPE_SEQUENCE;

null: NULL;

boolean: BOOLEAN;

name: NAME | BLOCKED_NAME;

integer: INTEGER;

number: NUMBER;

funcName: NAME | CONST;

func : funcName LEFT_PARENTHESIS (expr (COMMA expr)*)? RIGHT_PARENTHESIS;
