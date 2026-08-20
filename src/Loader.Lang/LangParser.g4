parser grammar LangParser;
options { tokenVocab=LangLexer; }

start:
    expr EOF;

full_statement:
    statement EOF;

full_script:
    statement+ EOF;


statement
    : load_statement
    | drop_statement;

drop_statement
    : DROP name SEMICOLON
    ;

load_statement
    : load_table_name
    load_first?
    load_kind?
    LOAD load_fields
    FROM load_source
    (
        load_sql
        |
        load_where?
        load_group_by?
        load_order_by?
        load_limit?
    )
    SEMICOLON
    ;

load_table_name
    : name COLON
    ;

load_first
    : FIRST INTEGER
    ;

load_kind
    : TEMP
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

load_sql
    : SQL SQL_TEXT?
    ;

source_call
    : NAME LEFT_PARENTHESIS (inline_data | option_list)? RIGHT_PARENTHESIS
    ;

load_source
    : source_call
    | source_table
    ;

source_table
    : name
    ;

option_list
    : load_option (COMMA load_option)* COMMA?
    ;

load_option
    : NAME EQUAL option_literal
    | option_literal
    ;

option_literal
    : string
    | name
    | integer
    | number
    | boolean
    ;

inline_data
    : inline_header SEMICOLON inline_row (SEMICOLON inline_row)* SEMICOLON?
    ;

inline_header
    : name (COMMA name)* COMMA?
    ;

inline_row
    : inline_value (COMMA inline_value)* COMMA?
    ;

inline_value
    : string
    | inline_number
    | inline_integer
    | boolean
    | null
    ;

inline_integer
    : MINUS? INTEGER
    ;

inline_number
    : MINUS? NUMBER
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

name: NAME | BLOCKED_NAME | FIRST | TEMP;

integer: INTEGER;

number: NUMBER;

funcName: NAME;

func : funcName LEFT_PARENTHESIS (expr (COMMA expr)*)? RIGHT_PARENTHESIS;
