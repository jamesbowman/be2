3   constant columns

: n. ( n -- )   base @ swap decimal 0 u.r base ! ;
: csi           27 emit '[' emit ;
: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;
: hide          csi ." ?25l" ;
: cursor        csi ." ?25h" ;
: el            csi ." K" ;

form constant w 1- constant h
w columns / constant LW
0 value nl  variable pos 0 pos !

create ff       256 LW * allot
: lines ( u -- u )      LW * ;
: line  ( n -- a )      lines ff + ;
: line$ ( n -- u )      line LW -trailing ;
: isblank ( n -- f )
    true swap
    line LW bounds do i c@ bl = and loop ;
: cur                   ff pos @ + ;
: $ nl 1- ;

create linebuf  LW allot

: e
    parse-name
    ff 256 lines blank
    r/o open-file throw >r
    0
    begin
        linebuf LW r@ read-line throw
    while ( lnum n )
        >r linebuf over line r> move
        1+
    repeat
    drop to nl
    nl . ."  lines read" cr
    r> drop ;

: w
    parse-name
    w/o create-file throw >r
    nl 0 begin
        2dup <>
    while
        dup line$ r@ write-line throw
        1+
    repeat
    r> drop 2drop ;

: xy ( row col -- x y )
    over h / LW * +
    swap h mod ;

: lnum  pos @ LW / ;
: cnum  pos @ LW mod ;
: rowcol pos @ LW /mod swap ;

: redraw
    255 fg
    nl 0 do
        i 0 xy at-xy
        i line LW type
    loop
    lnum cnum xy at-xy ;

: def   1 max ;

\ an addr. is either a char or line address:
\   p 0     char address p
\   p 1     line address p

: rel   pos @ + ;
: col0  dup LW mod - ;
: >addr ( row col -- addr. )  swap lines + 0 ;
: backward-brace ( n -- line# )
    drop
    0 lnum
    begin
        1- 0 max
        2dup =
        over isblank or
    until
    nip ;
: forward-brace ( n -- line# )
    drop
    $ lnum
    begin
        1+
        2dup =
        over isblank or
    until
    nip ;
: backward-word ( a0 -- a1 )
    begin 1- dup c@ bl <> until
    begin 1- dup c@ bl = until 1+ ;
: forward-word ( a0 -- a1 )
    begin dup c@ bl <> while 1+ repeat
    begin dup c@ bl =  while 1+ repeat ;
: end-word
    begin 1+ dup c@ bl <> until
    begin 1+ dup c@ bl = until 1- ;
: times ( n xt -- a1 )
    cur
    rot def 0 do
        over execute
    loop nip ff - 0 ;
: mv ( n key -- addr. ) \ n key as a movement
    case
    'G'     of dup 0= nl and + 1- lines 1 endof
    'M'     of drop h 3 2 */ lines 1 endof
    'j'     of def lines rel 1 endof
    'k'     of def LW negate * rel 1 endof
    13      of def lines rel col0 1 endof
    bl      of 1 rel 0 endof
    'h'     of -1 rel 0 endof
    'l'     of 1 rel 0 endof
    'b'     of ['] backward-word times endof
    'e'     of ['] end-word times endof
    'w'     of ['] forward-word times endof
    '|'     of lnum swap >addr endof
    '0'     of drop lnum 0 >addr endof
    '^'     of drop lnum 0 >addr endof
    '$'     of drop lnum dup line$ nip dup 0> + >addr endof
    '}'     of forward-brace lines 1 endof
    '{'     of backward-brace lines 1 endof
            . abort
    endcase ;

: go ( addr. -- )
    drop
    dup 0 nl lines within
    if pos ! else drop then ;

: between   1+ within ;
: tolower   dup 'A' 'Z' between $20 and - ;
: isalpha   tolower 'a' 'z' between ;
: isnumber ( n key -- n key f )
    over 0<> over '0' = and
    over '1' '9' 1+ within or ;

: loc
    0 begin
        key isnumber if
            '0' - swap 10 * +
        else
            mv exit
        then
    again ;

: 2sort 2dup > if swap then ;

: d ( addr. )
    if
        page
        LW / lnum 2sort ( lo hi )
        over line over line swap ( hi lo )
        over nl line swap - move
        - nl + to nl
    else
        abort
    then ;

: visual
    page 0
    begin
        hide redraw cursor
        key
        isnumber if
            '0' - swap 10 * +
        else
            case
            'd'     of drop loc d endof
            ':'     of 0 h 1- at-xy quit endof
            '~'     of cur dup c@ isalpha $20 and
                       xor swap c! 1 rel 0 go endof
            27      of page bye endof
                    mv go 0
            endcase 0
        then
    again ;

: v visual ;
: q bye ;

e te.fs v
w tmp
