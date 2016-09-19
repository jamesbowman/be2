\ ........................................................!....!
3   constant columns
80  constant LW

create ff       256 LW * allot
: line  ( n -- a )      LW * ff + ;
: line$ ( n -- u )      line LW -trailing ;
: isblank ( n -- f )
    true swap
    line LW bounds do i c@ bl = and loop ;

create linebuf  65 allot

: n. ( n -- )   base @ swap decimal 0 u.r base ! ;
: csi           27 emit '[' emit ;
: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;
: hide          csi ." ?25l" ;
: cursor        csi ." ?25h" ;
: el            csi ." K" ;

form constant w 1- constant h
w columns / constant qw
0 value nl  variable pos 0 pos !
: cur                   ff pos @ + ;
: $ nl 1- ;

: e
    parse-name
    ff 256 LW * blank
    r/o open-file throw >r
    0
    begin
        linebuf 65 r@ read-line throw
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
    over h / qw * +
    swap h mod ;

: lnum  pos @ LW / ;
: cnum  pos @ LW mod ;
: rowcol pos @ LW /mod swap ;

: redraw
    154 fg
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
: >addr ( row col -- addr. )  swap LW * + 0 ;
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
: mv ( n key -- addr. ) \ n key as a movement
    case
    'G'     of dup 0= nl and + 1- LW * 1 endof
    'j'     of def LW * rel 1 endof
    'k'     of def LW negate * rel 1 endof
    9       of pos @ 4 + -4 and 0 endof
    13      of def LW * rel col0 1 endof
    bl      of 1 rel 0 endof
    'h'     of -1 rel 0 endof
    'l'     of 1 rel 0 endof
    '|'     of lnum swap >addr endof
    '0'     of drop lnum 0 >addr endof
    '^'     of drop lnum 0 >addr endof
    '$'     of drop lnum dup line$ nip dup 0> + >addr endof
    '}'     of forward-brace LW * 1 endof
    '{'     of backward-brace LW * 1 endof
            . abort
    endcase ;

: go ( addr. -- )
    drop
    dup 0 nl LW * within
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
            'q'     of page exit endof
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
