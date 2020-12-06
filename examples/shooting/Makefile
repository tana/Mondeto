.PHONY: all copy clean

all: raygun.wasm bullet.wasm target.wasm

WASM_FILES = raygun.wasm bullet.wasm target.wasm
COPY_DEST = ../../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

clean:
	rm $(WASM_FILES)

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts

bullet.wasm: bullet.ts

target.wasm: target.ts