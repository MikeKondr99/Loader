lexer grammar LangLexer;

LOAD: [Ll] [Oo] [Aa] [Dd];
AS: [Aa] [Ss];
FROM: [Ff] [Rr] [Oo] [Mm];
WHERE: [Ww] [Hh] [Ee] [Rr] [Ee];


AND: [Aa] [Nn] [Dd];
OR: [Oo] [Rr];
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

BOOLEAN: [Tt] [Rr] [Uu] [Ee] | [Ff] [Aa] [Ll] [Ss] [Ee];

NULL: [Nn] [Uu] [Ll] [Ll];

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
