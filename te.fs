\ ........................................................!....!
3   constant columns
80  constant LW

create ff       256 LW * allot 
: line  ( n -- a )    LW * ff + ;
: line$ ( n -- a u )
    line LW -trailing nip ;

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

: rel  pos @ + ;
: >addr ( row col -- addr. )  swap LW * + 0 ;
: mv ( n key -- addr. ) \ n key as a movement
    case
    'G'     of dup 0= nl and + 1- LW * 1 endof
    'j'     of def LW * rel 1 endof
    'k'     of def LW negate * rel 1 endof
    9       of pos @ 4 + -4 and 0 endof
    bl      of 1 rel 0 endof
    'h'     of -1 rel 0 endof
    'l'     of 1 rel 0 endof
    '|'     of lnum swap >addr endof
    '^'     of drop lnum 0 >addr endof
    '$'     of drop lnum dup line$ dup 0> + >addr endof
            . abort
    endcase ;

: go ( addr. -- )
    drop
    dup 0 $ LW * within
    if pos ! else drop then ;

: isnumber ( n key -- n key f )
    over 0<> over '0' = and
    over '1' '9' 1+ within or ;

: visual
    page 0
    begin
        hide redraw cursor
        key
        isnumber if
            '0' - swap 10 * +
        else
            case
            ':'     of 0 h 1- at-xy quit endof
            'q'     of page exit endof
            27      of page bye endof
                    mv go 0
            endcase 0
        then
    again ;

: v visual ;
: q bye ;

e te.fs v
