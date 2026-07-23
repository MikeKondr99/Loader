parser grammar LangParser;
options { tokenVocab=LangLexer; }

start:
    expr EOF;

full_statement:
    statement EOF;

full_script:
    statement+ EOF;


statement
    : load_statement;

load_statement
    : load_table_name?
    LOAD load_fields
    FROM BLOCKED_NAME source_options?
    load_where?
    load_group_by?
    load_order_by?
    load_limit?
    SEMICOLON
    ;

load_table_name
    : NAME COLON
    ;

load_where
    : WHERE expr
    ;

load_group_by
    : GROUP BY expr (COMMA expr)* COMMA?
    ;

load_order_by
    : ORDER BY order_by_field (COMMA order_by_field)* COMMA?
    ;

order_by_field
    : expr order_direction?
    ;

order_direction
    : ASC
    | DESC
    ;

load_limit
    : LIMIT INTEGER load_offset?
    ;

load_offset
    : OFFSET INTEGER
    ;

source_options
    : LEFT_PARENTHESIS option_list? RIGHT_PARENTHESIS
    ;

option_list
    : load_option (COMMA load_option)* COMMA?
    ;

load_option
    : NAME
    | NAME EQUAL option_literal
    ;

option_literal
    : string
    | integer
    | number
    | boolean
    ;

load_fields
    : load_all_fields
    | load_field (COMMA load_field)* COMMA?
    ;

load_all_fields
    : MUL
    ;

load_field
    : expr AS name
    | name
    ;

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

funcName: NAME;

func : funcName LEFT_PARENTHESIS (expr (COMMA expr)*)? RIGHT_PARENTHESIS;
