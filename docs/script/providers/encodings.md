# Encodings

Файловые provider-ы с параметром `encoding` принимают стандартные имена .NET `Encoding`.

Короткие aliases не поддерживаются специально. Например, вместо `cp1251` используйте `windows-1251`.

Пример:

```ts
orders:
LOAD *
FROM Csv('orders.csv', encoding='windows-1251');
```

## Поддерживаемые имена

| Encoding | Code page | Описание |
| --- | --- | --- |
| `ASMO-708` | `708` | Arabic (ASMO 708) |
| `big5` | `950` | Chinese Traditional (Big5) |
| `cp1025` | `21025` | IBM EBCDIC (Cyrillic Serbian-Bulgarian) |
| `cp866` | `866` | Cyrillic (DOS) |
| `cp875` | `875` | IBM EBCDIC (Greek Modern) |
| `csISO2022JP` | `50221` | Japanese (JIS-Allow 1 byte Kana) |
| `DOS-720` | `720` | Arabic (DOS) |
| `DOS-862` | `862` | Hebrew (DOS) |
| `EUC-CN` | `51936` | Chinese Simplified (EUC) |
| `EUC-JP` | `20932` | Japanese (JIS 0208-1990 and 0212-1990) |
| `euc-jp` | `51932` | Japanese (EUC) |
| `euc-kr` | `51949` | Korean (EUC) |
| `GB18030` | `54936` | Chinese Simplified (GB18030) |
| `gb2312` | `936` | Chinese Simplified (GB2312) |
| `hz-gb-2312` | `52936` | Chinese Simplified (HZ) |
| `IBM00858` | `858` | OEM Multilingual Latin I |
| `IBM00924` | `20924` | IBM Latin-1 |
| `IBM01047` | `1047` | IBM Latin-1 |
| `IBM01140` | `1140` | IBM EBCDIC (US-Canada-Euro) |
| `IBM01141` | `1141` | IBM EBCDIC (Germany-Euro) |
| `IBM01142` | `1142` | IBM EBCDIC (Denmark-Norway-Euro) |
| `IBM01143` | `1143` | IBM EBCDIC (Finland-Sweden-Euro) |
| `IBM01144` | `1144` | IBM EBCDIC (Italy-Euro) |
| `IBM01145` | `1145` | IBM EBCDIC (Spain-Euro) |
| `IBM01146` | `1146` | IBM EBCDIC (UK-Euro) |
| `IBM01147` | `1147` | IBM EBCDIC (France-Euro) |
| `IBM01148` | `1148` | IBM EBCDIC (International-Euro) |
| `IBM01149` | `1149` | IBM EBCDIC (Icelandic-Euro) |
| `IBM037` | `37` | IBM EBCDIC (US-Canada) |
| `IBM1026` | `1026` | IBM EBCDIC (Turkish Latin-5) |
| `IBM273` | `20273` | IBM EBCDIC (Germany) |
| `IBM277` | `20277` | IBM EBCDIC (Denmark-Norway) |
| `IBM278` | `20278` | IBM EBCDIC (Finland-Sweden) |
| `IBM280` | `20280` | IBM EBCDIC (Italy) |
| `IBM284` | `20284` | IBM EBCDIC (Spain) |
| `IBM285` | `20285` | IBM EBCDIC (UK) |
| `IBM290` | `20290` | IBM EBCDIC (Japanese katakana) |
| `IBM297` | `20297` | IBM EBCDIC (France) |
| `IBM420` | `20420` | IBM EBCDIC (Arabic) |
| `IBM423` | `20423` | IBM EBCDIC (Greek) |
| `IBM424` | `20424` | IBM EBCDIC (Hebrew) |
| `IBM437` | `437` | OEM United States |
| `IBM500` | `500` | IBM EBCDIC (International) |
| `ibm737` | `737` | Greek (DOS) |
| `ibm775` | `775` | Baltic (DOS) |
| `ibm850` | `850` | Western European (DOS) |
| `ibm852` | `852` | Central European (DOS) |
| `IBM855` | `855` | OEM Cyrillic |
| `ibm857` | `857` | Turkish (DOS) |
| `IBM860` | `860` | Portuguese (DOS) |
| `ibm861` | `861` | Icelandic (DOS) |
| `IBM863` | `863` | French Canadian (DOS) |
| `IBM864` | `864` | Arabic (864) |
| `IBM865` | `865` | Nordic (DOS) |
| `ibm869` | `869` | Greek, Modern (DOS) |
| `IBM870` | `870` | IBM EBCDIC (Multilingual Latin-2) |
| `IBM871` | `20871` | IBM EBCDIC (Icelandic) |
| `IBM880` | `20880` | IBM EBCDIC (Cyrillic Russian) |
| `IBM905` | `20905` | IBM EBCDIC (Turkish) |
| `IBM-Thai` | `20838` | IBM EBCDIC (Thai) |
| `iso-2022-jp` | `50220` | Japanese (JIS) |
| `iso-2022-jp` | `50222` | Japanese (JIS-Allow 1 byte Kana - SO/SI) |
| `iso-2022-kr` | `50225` | Korean (ISO) |
| `iso-8859-1` | `28591` | Western European (ISO) |
| `iso-8859-13` | `28603` | Estonian (ISO) |
| `iso-8859-15` | `28605` | Latin 9 (ISO) |
| `iso-8859-2` | `28592` | Central European (ISO) |
| `iso-8859-3` | `28593` | Latin 3 (ISO) |
| `iso-8859-4` | `28594` | Baltic (ISO) |
| `iso-8859-5` | `28595` | Cyrillic (ISO) |
| `iso-8859-6` | `28596` | Arabic (ISO) |
| `iso-8859-7` | `28597` | Greek (ISO) |
| `iso-8859-8` | `28598` | Hebrew (ISO-Visual) |
| `iso-8859-8-i` | `38598` | Hebrew (ISO-Logical) |
| `iso-8859-9` | `28599` | Turkish (ISO) |
| `Johab` | `1361` | Korean (Johab) |
| `koi8-r` | `20866` | Cyrillic (KOI8-R) |
| `koi8-u` | `21866` | Cyrillic (KOI8-U) |
| `ks_c_5601-1987` | `949` | Korean |
| `macintosh` | `10000` | Western European (Mac) |
| `shift_jis` | `932` | Japanese (Shift-JIS) |
| `us-ascii` | `20127` | US-ASCII |
| `utf-16` | `1200` | Unicode |
| `utf-16BE` | `1201` | Unicode (Big-Endian) |
| `utf-32` | `12000` | Unicode (UTF-32) |
| `utf-32BE` | `12001` | Unicode (UTF-32 Big-Endian) |
| `utf-7` | `65000` | Unicode (UTF-7) |
| `utf-8` | `65001` | Unicode (UTF-8) |
| `windows-1250` | `1250` | Central European (Windows) |
| `windows-1251` | `1251` | Cyrillic (Windows) |
| `Windows-1252` | `1252` | Western European (Windows) |
| `windows-1253` | `1253` | Greek (Windows) |
| `windows-1254` | `1254` | Turkish (Windows) |
| `windows-1255` | `1255` | Hebrew (Windows) |
| `windows-1256` | `1256` | Arabic (Windows) |
| `windows-1257` | `1257` | Baltic (Windows) |
| `windows-1258` | `1258` | Vietnamese (Windows) |
| `windows-874` | `874` | Thai (Windows) |
| `x-Chinese-CNS` | `20000` | Chinese Traditional (CNS) |
| `x-Chinese-Eten` | `20002` | Chinese Traditional (Eten) |
| `x-cp20001` | `20001` | TCA Taiwan |
| `x-cp20003` | `20003` | IBM5550 Taiwan |
| `x-cp20004` | `20004` | TeleText Taiwan |
| `x-cp20005` | `20005` | Wang Taiwan |
| `x-cp20261` | `20261` | T.61 |
| `x-cp20269` | `20269` | ISO-6937 |
| `x-cp20936` | `20936` | Chinese Simplified (GB2312-80) |
| `x-cp20949` | `20949` | Korean Wansung |
| `x-cp50227` | `50227` | Chinese Simplified (ISO-2022) |
| `x-EBCDIC-KoreanExtended` | `20833` | IBM EBCDIC (Korean Extended) |
| `x-Europa` | `29001` | Europa |
| `x-IA5` | `20105` | Western European (IA5) |
| `x-IA5-German` | `20106` | German (IA5) |
| `x-IA5-Norwegian` | `20108` | Norwegian (IA5) |
| `x-IA5-Swedish` | `20107` | Swedish (IA5) |
| `x-iscii-as` | `57006` | ISCII Assamese |
| `x-iscii-be` | `57003` | ISCII Bengali |
| `x-iscii-de` | `57002` | ISCII Devanagari |
| `x-iscii-gu` | `57010` | ISCII Gujarati |
| `x-iscii-ka` | `57008` | ISCII Kannada |
| `x-iscii-ma` | `57009` | ISCII Malayalam |
| `x-iscii-or` | `57007` | ISCII Oriya |
| `x-iscii-pa` | `57011` | ISCII Punjabi |
| `x-iscii-ta` | `57004` | ISCII Tamil |
| `x-iscii-te` | `57005` | ISCII Telugu |
| `x-mac-arabic` | `10004` | Arabic (Mac) |
| `x-mac-ce` | `10029` | Central European (Mac) |
| `x-mac-chinesesimp` | `10008` | Chinese Simplified (Mac) |
| `x-mac-chinesetrad` | `10002` | Chinese Traditional (Mac) |
| `x-mac-croatian` | `10082` | Croatian (Mac) |
| `x-mac-cyrillic` | `10007` | Cyrillic (Mac) |
| `x-mac-greek` | `10006` | Greek (Mac) |
| `x-mac-hebrew` | `10005` | Hebrew (Mac) |
| `x-mac-icelandic` | `10079` | Icelandic (Mac) |
| `x-mac-japanese` | `10001` | Japanese (Mac) |
| `x-mac-korean` | `10003` | Korean (Mac) |
| `x-mac-romanian` | `10010` | Romanian (Mac) |
| `x-mac-thai` | `10021` | Thai (Mac) |
| `x-mac-turkish` | `10081` | Turkish (Mac) |
| `x-mac-ukrainian` | `10017` | Ukrainian (Mac) |
