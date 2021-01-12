.PHONY: all copy clean

all: beep.wasm

WASM_FILES = beep.wasm
COPY_DEST = ../../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

clean:
	rm $(WASM_FILES)

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

beep.wasm: beep.ts
