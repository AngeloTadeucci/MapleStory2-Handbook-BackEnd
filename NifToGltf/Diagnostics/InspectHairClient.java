import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.util.HashSet;

// Read-only inspection of hair XML consumers in an existing client analysis.
public class InspectHairClient extends GhidraScript {
    public void run() throws Exception {
        DecompInterface decompiler = new DecompInterface();
        decompiler.openProgram(currentProgram);
        String[] args = getScriptArgs();
        if (args.length > 0) {
            for (String arg : args) {
                if (arg.startsWith("floats:")) {
                    var base = toAddr(arg.substring(7));
                    for (int i = 0; i < 4; i++) println("FLOAT " + base.add(i * 4) + " " + Float.intBitsToFloat(getInt(base.add(i * 4))));
                } else if (arg.startsWith("namespace:")) {
                    Function member = getFunctionAt(toAddr(arg.substring(10)));
                    SymbolIterator members = currentProgram.getSymbolTable().getSymbols(member.getParentNamespace());
                    while (members.hasNext()) {
                        Symbol symbol = members.next();
                        println("MEMBER " + symbol.getAddress() + " " + symbol.getName(true));
                    }
                } else if (arg.startsWith("table:")) {
                    var base = toAddr(arg.substring(6));
                    for (int i = 0; i < 48; i++) {
                        long value = getLong(base.add(i * 8));
                        Function entry = getFunctionAt(toAddr(value));
                        println("TABLE " + base.add(i * 8) + " " + Long.toHexString(value) + " " + entry);
                    }
                } else if (arg.startsWith("string:")) {
                    String needle = arg.substring(7).toLowerCase();
                    DataIterator values = currentProgram.getListing().getDefinedData(true);
                    while (values.hasNext() && !monitor.isCancelled()) {
                        Data value = values.next();
                        if (value.getValue() instanceof String && value.getValue().toString().toLowerCase().contains(needle)) {
                            println("MATCH STRING " + value.getAddress() + " " + value.getValue());
                            for (Reference reference : getReferencesTo(value.getAddress()))
                                println("STRING CALLER " + reference.getFromAddress() + " " + getFunctionContaining(reference.getFromAddress()));
                        }
                    }
                } else if (arg.startsWith("0x") || arg.startsWith("containing:")) {
                    Function function = arg.startsWith("containing:")
                        ? getFunctionContaining(toAddr(arg.substring(11))) : getFunctionAt(toAddr(arg));
                    if (function == null) {
                        for (Reference reference : getReferencesTo(toAddr(arg))) {
                            Function caller = getFunctionContaining(reference.getFromAddress());
                            println("DATA XREF " + reference.getFromAddress() + " " + caller);
                            if (caller != null) {
                                var result = decompiler.decompileFunction(caller, 60, monitor);
                                if (result.decompileCompleted()) println(result.getDecompiledFunction().getC());
                            }
                        }
                    }
                    if (function != null) {
                        println("HAIR FUNCTION " + function.getEntryPoint() + " " + function.getName());
                        var result = decompiler.decompileFunction(function, 60, monitor);
                        if (result.decompileCompleted()) println(result.getDecompiledFunction().getC());
                        for (Reference reference : getReferencesTo(function.getEntryPoint()))
                            println("CALLER " + reference.getFromAddress() + " " + getFunctionContaining(reference.getFromAddress()));
                    }
                } else {
                    SymbolIterator symbols = currentProgram.getSymbolTable().getAllSymbols(true);
                    while (symbols.hasNext()) {
                        Symbol symbol = symbols.next();
                        if (symbol.getName().toLowerCase().contains(arg.toLowerCase()))
                            println("HAIR SYMBOL " + symbol.getAddress() + " " + symbol.getName(true));
                    }
                }
            }
            decompiler.dispose();
            return;
        }
        HashSet<String> seen = new HashSet<>();
        DataIterator data = currentProgram.getListing().getDefinedData(true);
        while (data.hasNext() && !monitor.isCancelled()) {
            Data value = data.next();
            Object raw = value.getValue();
            if (!(raw instanceof String)) continue;
            String text = (String) raw;
            if (!(text.equals("jointangle") || text.equals("posz") || text.equals("negz") || text.contains("UConstraint"))) continue;
            println("HAIR STRING " + value.getAddress() + " " + text);
            for (Reference reference : getReferencesTo(value.getAddress())) {
                Function function = getFunctionContaining(reference.getFromAddress());
                if (function == null || !seen.add(function.getEntryPoint().toString())) continue;
                println("HAIR FUNCTION " + function.getEntryPoint() + " " + function.getName());
                var result = decompiler.decompileFunction(function, 40, monitor);
                if (result.decompileCompleted()) println(result.getDecompiledFunction().getC());
            }
        }
        decompiler.dispose();
    }
}
