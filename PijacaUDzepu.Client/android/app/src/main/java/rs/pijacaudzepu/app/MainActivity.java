package rs.pijacaudzepu.app;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {

    private static volatile boolean inForeground = false;

    static boolean isInForeground() {
        return inForeground;
    }

    @Override
    public void onResume() {
        super.onResume();
        inForeground = true;
        AlarmMessagingService.cancelAllAlarms(this);
    }

    @Override
    public void onPause() {
        inForeground = false;
        super.onPause();
    }
}
