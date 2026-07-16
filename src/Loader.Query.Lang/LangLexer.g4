lexer grammar LangLexer;

AND: [a] [n] [d];
OR: [o] [r];
CONST: [c] [o] [n] [s] [t];
LET: [l] [e] [t];
LEFT_PARENTHESIS: '(';
RIGHT_PARENTHESIS: ')';
HAT: '^';
PLUS: '+';
MINUS: '-';
MUL: '*';
DIV: '/';
LESS_EQUAL: '<=';
LESS_THEN: '<';
GREATER_EQUAL: '>=';
GREATER_THEN: '>';
EQUAL: '=';
NOT_EQUAL: '!=';
DOT: '.';
COMMA: ',';
SEMICOLON: ';';
QUOTE: ['] -> pushMode(IN_STRING);
CURLY_CLOSE: '}' -> popMode;

BOOLEAN: 'true' | 'false';

NULL: 'null';

NAME: [a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я_0-9]*;

BLOCKED_NAME: '[' (ESCAPED_BLOCKED_NAME | ~']')+? ']';

INTEGER: [0-9]+;

NUMBER: ([0-9]* '.' [0-9]+) | ([0-9]+ '.' [0-9]+);

LINE_COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '/*' .*? '*/' -> skip;

WS: [ \t\r\n]+ -> skip;

fragment ESCAPED_BLOCKED_NAME: '\\]';

mode IN_STRING;

DQUOTE_IN_STRING: ['] -> type(QUOTE), popMode;

CURLY_OPEN: '${' -> pushMode(DEFAULT_MODE);

ESCAPE_SEQUENCE: '\\' . ;

TEXT: ~[\\'$]+ | '$';
