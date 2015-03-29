#! /usr/bin/gforth
( vim: set syntax=colorforth: )

\ be2 - a.k.a. Block Editor II

\ An elegant editor for a more civilized age.
\ This block editor attempts to emphasize the virtues of Forth blocks,
\ while interoperating with modern systems.















( Block size constants )

80 constant columns      24 constant lines
1024 constant block-size
columns 1+ constant columns'     12 constant lines-per-block
lines constant pgup-size


255 constant continue-char     \ '\' Line continuation
254 constant start-file-char   \ '$' Immediately followed by filename
253 constant end-file-char     \ '~'
201 constant color-char-first  \ 1-15 from this are color escape codes
215 constant color-char-last
color-char-last color-char-first - 1+ constant color-char-count

4096 constant max-path
10 constant lf







( Editor state )

variable caret  \ relative to top of screen
variable top  \ relative to top of file
variable input-mode  \ f = navigate, t = input



















( Terminal handling )
: n. ( n -- ) base @ swap 255 and s>d decimal <# #s #> type base ! ;
: esc   27 emit ;
: normal   esc ." [0m" ;

: fg   esc ." [38;5;" n. ." m" ;     : bg   esc ." [48;5;" n. ." m" ;

: clear-to-end   esc ." [0K" ;

: scroll-down   esc ." D" ;     : scroll-up   esc ." M" ;

: hide   esc ." [?25l" ;     : show   esc ." [?25h" ;

: ecr   lf stderr emit-file drop ;
: etype ( a n -- ) stderr write-file drop ;


\ Terminal state
: screen-columns ( -- n ) form nip ;
: screen-lines ( -- n ) form drop ;
: drawn-columns ( -- n ) columns screen-columns min ;



( Background colors )
: body-color   255 fg 0 bg ;
: title-color   255 fg 235 bg ;
: margin-color   255 fg 238 bg ;
: prompt-color   0 fg 7 bg ;



















( Character and string handling words )

: in-range? ( n a b -- f ) >r over r> <= -rot >= and ;
: alpha? ( n -- f ) dup [char] A [char] Z in-range? >r
                        [char] a [char] z in-range? r> or ;
: decimal-digit? ( n -- f ) [char] 0 [char] 9 in-range? ;
: alpha-numeric? ( n -- f ) dup alpha? swap decimal-digit? or ;
: identifier-char? ( n -- f ) dup alpha-numeric? over [char] _ = or
                              over [char] - = or swap [char] / = or ;
: space? ( n -- f ) bl = ;

: replace-char ( ch a b -- ch ) -rot over = if drop else nip then ;
: normalize-char ( ch -- ch ) [char] ) [char] ( replace-char
                              [char] ] [char] [ replace-char
                              [char] } [char] { replace-char
                              [char] > [char] < replace-char
                              [char] / [char] * replace-char
                              [char] ; [char] : replace-char ;
: make-printable ( ch -- ch ) continue-char   [char] \ replace-char
                              start-file-char [char] $ replace-char
                              end-file-char   [char] ~ replace-char ;

: rtrim ( a n -- a n ) begin 2dup + 1- c@ bl = over 0<> and while 1- repeat ;

( Word hash computation )

variable hash-value

hex ffffffff decimal constant max32
: mask32 ( n -- n ) max32 and ;
: word-hash-reset   0 hash-value ! ;
: +word-hash ( ch -- )
  dup [char] - = if drop exit then
  normalize-char hash-value @ + mask32
  dup 10 lshift mask32 + mask32
  dup 6 rshift xor hash-value ! ;

: word-hash ( -- n ) hash-value @ dup 3 lshift mask32 +
                     dup 11 rshift xor dup 15 lshift mask32 + mask32 ;

\ Word hash color
: weight ( n -- n ) 6 /mod 6 /mod + + ;
: 5flip ( n -- n ) 5 swap - ;
: flip ( n -- n ) 6 /mod 6 /mod 5flip 6 * swap 5flip + 6 * swap 5flip + ;
: word-hash-color ( -- n )
  word-hash 216 mod dup weight 6 < if flip then 16 + ;


( Color Buffer )

create color-buffer columns allot
variable color-buffer-length
: color-buffer@ ( -- a n ) color-buffer color-buffer-length @ ;
: color-buffer-reset   0 color-buffer-length ! ;
: +color-buffer ( n -- )
  dup +word-hash
  color-buffer color-buffer-length @ + c!
  1 color-buffer-length +!  ;
: cb= ( a n -- f ) color-buffer@ str= ;

\ Color State
variable color-pos
variable forthy
variable color-stuck
: stick-color   word-hash-color color-stuck ! ;
: unstick-color   0 color-stuck ! ;
: fixup-color ( -- n ) color-stuck @ dup 0= if drop word-hash-color then ;
: cb-quote? ( -- f ) color-buffer@ 1 = swap c@ [char] " = and ;




( Dump accumulated color text )

: type-printable ( a n -- ) 0 ?do dup i + c@ make-printable emit loop drop ;

: color-dump
  fixup-color fg
  color-buffer@ type-printable
  cb-quote? if
    color-stuck @ word-hash-color = if unstick-color else stick-color then
  then
  s" *"  cb= color-pos @ 0 = and if stick-color then
  s" :"  cb= color-pos @ 0 = and if true forthy ! then
  s" //" cb= if stick-color then
  s" ("  cb= forthy @ color-pos @ 0= or and if stick-color then
  s" )"  cb= forthy @ and if unstick-color then
  s" #"  cb= if stick-color then
  s" \"  cb= if stick-color then
  s" "   cb= 0= if 1 color-pos +! then
  color-buffer-reset   word-hash-reset
;




( Emit syntax highlighted characters )

: cemit ( n -- )
  dup color-char-first color-char-last in-range? if
    color-dump
    color-char-first - 1+ dup color-stuck ! fg bl emit exit
  then
  dup lf = if color-dump drop 0 color-stuck ! 0 color-pos ! 0 forthy ! exit then
  dup bl = if color-dump 7 fg emit exit then
  dup identifier-char? 0= if color-dump then
  dup +color-buffer
  identifier-char? 0= if color-dump then
;

: ctype ( a n -- ) 0 ?do dup i + c@ cemit loop drop lf cemit ;









( Command prompt handling )

create prompt-text columns allot
variable prompt-length

( workaround bug in history.fs )
defer old-everyline
action-of everyline is old-everyline
: flat-everyline   old-everyline prompt-length @ linew ! ;
' flat-everyline is everyline

: prompt! ( a n -- ) prompt-text swap dup prompt-length ! cmove ;
: prompt-buffer ( -- a n ) prompt-text columns ;
: goto-prompt ( -- a n ) 0 screen-lines 1- at-xy prompt-color clear-to-end ;
: setup-prompt ( -- a n ) goto-prompt prompt-buffer ;

: accept-cmd setup-prompt accept prompt-length ! ;
: edit-cmd setup-prompt prompt-length @ edit-line prompt-length ! ;
: do-cmd flush prompt-text prompt-length @ ['] evaluate catch drop ;





( Row access )

: block-uninit? ( n -- f ) block columns' + 1- c@ lf <> ;
: init1-block ( a -- )
  dup block-size bl fill
  lines-per-block 1- 0 do dup i 1+ columns' * 1- + lf swap c! loop
  block-size 1- + lf swap c!
  update ;
: init-page ( n -- ) 2/ 2* dup block init1-block 1+ block init1-block ;
: fixed-block ( n -- a ) dup block-uninit? if dup init-page then block ;
: row ( n -- a ) lines-per-block /mod fixed-block swap columns' * + ;


\ Row count

variable last-row-count  \ cached row count
: update-row-count   flush get-block-fid file-size throw d>s
                     block-size 2 * max
                     block-size / lines-per-block * last-row-count ! ;
: row-count ( -- n ) last-row-count @ ;

: top-row ( n -- a ) top @ + row-count mod row ;


( Margin handling )
variable margins-on
: margin-size ( -- n ) screen-columns drawn-columns - margins-on @ and ;
: lmargin ( -- n ) margin-size 2/ ;   : rmargin ( -- n ) margin-size dup 2/ - ;
: goto-lmargin ( n -- ) 0 swap at-xy ;
: goto-rmargin ( n -- ) columns lmargin + swap at-xy ;
: draw-left-margin ( n -- ) goto-lmargin margin-color lmargin spaces ;
: draw-right-margin ( n -- )
  rmargin if goto-rmargin margin-color bl emit clear-to-end then ;
: left-margin ( n -- ) margins-on @ if draw-left-margin else drop then ;
: right-margin ( n -- ) margins-on @ if draw-right-margin else drop then ;
: toggle-margins   margins-on @ 0= margins-on ! ;
\ Drawing rows
: row-title? ( n -- ) top @ + lines mod 0= ;
: row-color ( n -- ) row-title? if title-color else body-color then ;
: goto-row ( n -- ) lmargin swap at-xy ;
: row-string ( n -- a n ) top-row columns rtrim ;
: finish-row   7 fg clear-to-end ;  \ white foreground to show cursor
: print-row ( n -- ) dup goto-row dup row-color row-string ctype finish-row ;
: park-cursor   caret @ columns /mod swap lmargin + swap at-xy ;
: draw-row ( n -- ) dup left-margin dup print-row right-margin ;
: draw-rows ( n n -- ) do i draw-row loop ;
: caret-row ( -- n ) caret @ columns / ;

( Drawing regions )
variable refresh-level
1 constant cursor   2 constant full
: scrub ( n -- ) refresh-level @ max refresh-level ! ;
: unscrub   0 refresh-level ! ;
: line-refresh   hide caret-row draw-row show   cursor scrub ;
: full-refresh   hide screen-lines 0 draw-rows park-cursor show ;
: refresh refresh-level @ case cursor of park-cursor endof
                                 full of full-refresh endof endcase unscrub ;
: slide   margin-color page lines 0 draw-rows hide unscrub ;

\ Core cursor movement
: scroll-to-match ( n --  ) case 1 of scroll-down line-refresh endof
                                -1 of scroll-up   line-refresh endof
                                 full scrub endcase ;
: top+! ( n -- ) dup top @ + row-count mod top ! scroll-to-match ;
: fix-top   caret-row 0< if
                caret-row dup negate columns * caret +! top+! then
            caret-row screen-lines >= if
                caret-row screen-lines 1- - dup negate
                columns * caret +! top+! then ;
: caret+!  caret +! fix-top  cursor scrub ;
: line! ( n -- ) lines /mod lines * top ! columns * caret !  full scrub ;

( Caret handling )
: left   -1 caret+! ;              : right   1 caret+! ;
: up   columns negate caret+! ;    : down   columns caret+! ;
: page+! ( n -- ) lines * top+! ;
: type-cr   columns caret @ columns mod - caret+! ;


\ Access text under caret
: caret-column ( -- n ) caret @ columns mod ;
: caret-top ( -- n ) caret @ columns / top @ + row-count mod ;
: caret-at ( -- a ) caret-top row caret-column + ;

\ Move by words
: on-space? ( -- n ) caret-at c@ space? ;
: word-left
  left   begin on-space? 0= while left repeat
         begin on-space?    while left repeat   right ;
: word-right
         begin on-space?    while right repeat
         begin on-space? 0= while right repeat ;




( Line typing operations )
: type-char ( n -- ) caret-at c! update line-refresh right ;
: next-color ( n -- n )
  color-char-first - 1+ color-char-count mod color-char-first + ;
: color-cycle    caret-at c@ next-color caret-at c! update line-refresh ;
: ins-char   caret-at dup 1+ columns caret @ columns mod - 1- cmove>
             bl caret-at c! update line-refresh ;
: del-char   caret-at dup 1+ swap columns caret @ columns mod - 1- cmove
             update line-refresh ;















( Importation plumbing )
variable import-row  \ Row being written to
variable import-offset   \ Column being written to
: import-trails? ( -- f )
  import-row @ row columns rtrim nip import-offset @ <> ;
: import-pos ( -- a ) import-row @ row import-offset @ + ;
: import-cr   1 import-row +! 0 import-offset ! ;
: import-char-raw ( n -- ) import-pos c! 1 import-offset +! update ;
: import-wrap-check
  import-offset @ columns >= if
    -1 import-offset +!
    import-pos c@ continue-char import-char-raw
    import-cr import-char-raw
  then ;
: import-regular-char ( n -- ) import-wrap-check import-char-raw ;
: import-nl
  import-trails? if continue-char import-regular-char then import-cr ;
\ Public interface
: import-char ( n -- )
  dup lf = if drop import-nl else import-regular-char then ;
: start-import    row-count import-row ! 0 import-offset ! ;
: import-string ( a n -- ) 0 ?do dup c@ import-char 1+ loop drop ;
: import-cwd   s" PWD" getenv import-string ;

( Import file )
variable fid
create chunk block-size allot  \ Buffer for reading from file.
: import-file ( a n -- )
  start-import
  import-row @ >r
  2dup r/o open-file throw fid !
  start-file-char import-char
  import-cwd s" /" import-string import-string lf import-char
  begin
    chunk block-size fid @ read-file throw
    dup chunk swap import-string
  0= until
  fid @ close-file throw
  end-file-char import-char
  update-row-count
  r> top ! 0 caret !   full scrub
;






( File region words )

: row-has-eof? ( n -- f )
  row columns 0 do
    dup c@ end-file-char = if drop true unloop exit then 1+
  loop drop false ;
: find-file-end ( n -- n )
  begin dup row-count 1- < over row-has-eof? 0= and while 1+ repeat ;
: find-file-start ( n -- n )
  begin dup 0> over row c@ start-file-char <> and while 1- repeat ;














( Export row handling )
: last-char ( a n -- ch ) dup 0<> if + 1- c@ else 2drop 0 then ;
: line-fixup ( a n -- a n f )
  2dup last-char end-file-char = if 1- true exit then
  2dup last-char continue-char = if 1- false exit then
  2dup + lf swap c! 1+ false ;
: export-trim ( a n -- a n f ) rtrim line-fixup ;
: export-row ( -- f a n )
  import-row @ row chunk columns cmove
  chunk columns export-trim -rot
  1 import-row +! ;

\ Export filename handling
create filename max-path allot
variable filename-length
: +filename ( a n -- )
  filename filename-length @ + swap dup filename-length +! cmove ;
: filename@ ( -- a n ) filename filename-length @ ;
: export-filename
  0 filename-length !
  begin export-row +filename drop filename@ last-char lf = until
  filename@ 2 - swap 1+ swap ;


( Export implementation )

: export-file
  caret-top find-file-start import-row !
  export-filename 2dup w/o open-file if
    drop w/o create-file throw
  else nip nip then fid !
  begin
    export-row fid @ write-file throw
    import-row @ row-count >= or
  until
  fid @ close-file throw ;












( Dropping pages )

: block-count ( -- n ) row-count lines-per-block / ;
: drop-blocks ( end start -- )
  dup >r - block-count over - r> ?do
    dup i + block chunk block-size cmove
    chunk i block block-size cmove update
  loop flush
  block-size * negate
  get-block-fid file-size throw d>s + s>d
  get-block-fid resize-file throw
  update-row-count
  top @ row-count mod top !
  full scrub ;
: drop-file
  caret-top find-file-start
  dup 0= if drop exit then
  dup find-file-end
  lines / 1+ 2* swap lines / 2* drop-blocks ;
: drop-page   caret-top lines / 2* dup 2 + swap drop-blocks ;




( Searching )

: contains? ( a n a n -- f ) search nip nip ;
create search-text columns allot
variable search-length
: search! ( a n -- ) search-text swap dup search-length ! cmove ;
: search@ ( -- a n ) search-text search-length @ ;
: row-from-caret ( n -- a ) top @ caret-row + + row-count mod ;
: search-for
  row-count 0 do
    i 1+ row-from-caret row columns search@ contains? if
      i 1+ row-from-caret line!
      full scrub
      unloop
      exit
    then
  loop
  full scrub
;





( Page splitting )

create split-row columns allot  \ One line work buffer
: insert-page-rows
  caret-top row-count 1- ?do
    i row split-row columns cmove
    split-row lines i + row columns cmove update
  -1 +loop
;
: fill-page-rows ( ch -- )
  lines caret-top + caret-top do
    i row columns bl fill update
    dup i row c! update
  loop drop ;
: split-page
  insert-page-rows
  continue-char fill-page-rows
  update-row-count   full scrub
;





( Page navigation and insertion  )

: file-line! ( n -- ) caret-top find-file-start + row-count mod line! ;

: flush-top  caret-top lines / lines *
             row-count mod top ! 0 caret !  full scrub ;
: add-page
  flush-top
  insert-page-rows
  update-row-count
  lines top +!
  bl fill-page-rows
  full scrub ;











( Clipboard )
240 constant clipboard-limit
create clipboard clipboard-limit columns * allot
variable clipboard-rows
variable clipboard-last
: clipboard-row ( n -- a ) columns * clipboard + ;
: clipboard-next ( -- a ) clipboard-rows @ clipboard-row
                          1 clipboard-rows +! ;
: copy-line-raw
  caret-top clipboard-last @ <> if 0 clipboard-rows ! then
  clipboard-rows @ clipboard-limit >= if exit then
  caret-top row clipboard-next columns cmove
  cursor scrub ;
: copy-advance   type-cr caret-top clipboard-last ! ;
: clobber-line  caret-top row columns bl fill update line-refresh ;
: copy-line   copy-line-raw copy-advance ;
: cut-line   copy-line-raw clobber-line copy-advance ;
: paste
  clipboard-rows @ 0 ?do
    i clipboard-row caret-top row columns cmove update
    type-cr
  loop
  full scrub ;

( Shortcuts )

: parse-rest ( -- a n ) 127 parse ;
: e   parse-rest import-file ;
: s   parse-rest dup 0= if 2drop else search! then search-for ;
: g   parse-rest s>unumber? if d>s file-line! else 2drop then ;
: d   drop-page ;

















( Common keyboard handling )

: CTRL-   char [char] @ - postpone literal ; immediate

: common-key
  case
    k-left of left endof     k-right of right endof
    k-up of up endof         k-down of down endof
    k-prior of -1 page+! slide endof
    k-next of 1 page+! slide endof
    13 of type-cr endof  \ Enter
    CTRL- L of full scrub endof
    CTRL- W of color-cycle endof
  endcase
;









( Input mode )

: input-key
  dup common-key
  ekey>char if case
    127 of left bl type-char left endof
    9 of false input-mode ! endof
    27 of false input-mode ! endof
    CTRL- D of continue-char type-char endof
    CTRL- F of start-file-char type-char endof
    CTRL- G of end-file-char type-char endof
    dup bl 126 in-range? if dup type-char then
  endcase else drop then
;










( Navigate mode )
: navigate-key
  dup common-key
  ekey>char if case
    [char] h of left endof          [char] l of right endof
    [char] k of up endof            [char] j of down endof
    [char] n of word-left endof     [char] . of word-right endof
    [char] x of del-char endof      [char] z of ins-char endof
    [char] r of add-page endof      [char] c of split-page endof
    [char] y of flush-top endof  [char] b of 0 top ! 0 caret ! full scrub endof
    [char] W of export-file endof   [char] D of drop-file endof
    [char] C of copy-line endof     [char] V of paste endof
    [char] , of -1 page+! endof     [char] m of 1 page+! endof
    [char] X of cut-line endof
    [char] i of true input-mode ! endof
    [char] \ of accept-cmd do-cmd full scrub endof
    [char] | of edit-cmd do-cmd full scrub endof
    [char] / of s" s " prompt! edit-cmd do-cmd full scrub endof
    [char] ? of search-for endof
    CTRL- T  of toggle-margins full scrub endof
  endcase else drop then
;


( Main loop and startup )

: handle-key   input-mode @ if input-key else navigate-key then refresh ;
: editor   full-refresh begin ekey show handle-key again ;

: usage   s" Usage: be2 [search]" etype ecr   1 (bye) ;
: arg-check   argc @ 2 > if usage then ;

: block-filename ( -- a n)
  s" BE2_BLOCKS" getenv dup 0= if 2drop 0 filename-length !
      s" PWD" getenv +filename s" /.be2_blocks" +filename filename@ then ;
: setup-blocks
  0 block-offset !
  block-filename open-blocks
  update-row-count
  arg-check
  argc @ 2 >= if 1 arg search! search-for flush-top then
;
: main   setup-blocks editor ;
: reset-terminal   normal page ;
: go   ['] main catch flush dup -28 = if reset-terminal bye else throw then ;
go
