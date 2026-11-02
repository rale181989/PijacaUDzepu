package rs.pijacaudzepu.app;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.media.AudioAttributes;
import android.media.RingtoneManager;
import android.os.Build;
import android.service.notification.StatusBarNotification;
import androidx.annotation.NonNull;
import androidx.core.app.NotificationCompat;
import com.capacitorjs.plugins.pushnotifications.MessagingService;
import com.google.firebase.messaging.RemoteMessage;
import java.util.Map;

// Prima sve FCM poruke umesto servisa iz Capacitor push plugina i prosleđuje ih njemu (JS ih i dalje dobija).
// Poruke "alarm" (nova porudžbina za prodavca/kurira) prikazuje kao obaveštenje koje zvoni i vibrira
// u krug dok ga korisnik ne otvori ili dok server ne pošalje "alarm-cancel" sa istim tag-om.
public class AlarmMessagingService extends MessagingService {

    static final String CHANNEL_ID = "order_alarms";
    private static final int NOTIFICATION_ID = 1;
    // Ako niko ne reaguje, obaveštenje prestaje da zvoni posle 10 minuta
    private static final long TIMEOUT_MS = 10 * 60 * 1000;

    @Override
    public void onMessageReceived(@NonNull RemoteMessage remoteMessage) {
        Map<String, String> data = remoteMessage.getData();
        String type = data.get("type");
        String tag = data.get("tag");

        if ("alarm".equals(type) && tag != null) {
            // Dok je aplikacija na ekranu, zvoni sama aplikacija (SoundService)
            if (!MainActivity.isInForeground()) showAlarm(this, tag, data);
        } else if ("alarm-cancel".equals(type) && tag != null) {
            NotificationManager manager = getSystemService(NotificationManager.class);
            if (manager != null) manager.cancel(tag, NOTIFICATION_ID);
        }

        super.onMessageReceived(remoteMessage);
    }

    private static void showAlarm(Context context, String tag, Map<String, String> data) {
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager == null) return;
        ensureChannel(manager);

        // Ključ "google.message_id" je potreban da bi Capacitor plugin dodir prijavio kao
        // pushNotificationActionPerformed, pa klijent otvara stranicu iz "url"
        Intent intent = new Intent(context, MainActivity.class)
            .addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP)
            .putExtra("google.message_id", tag)
            .putExtra("url", data.get("url") != null ? data.get("url") : "");
        PendingIntent contentIntent = PendingIntent.getActivity(
            context, tag.hashCode(), intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        Notification notification = new NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(context.getApplicationInfo().icon)
            .setContentTitle(data.get("title"))
            .setContentText(data.get("body"))
            .setStyle(new NotificationCompat.BigTextStyle().bigText(data.get("body")))
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setCategory(NotificationCompat.CATEGORY_CALL)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setContentIntent(contentIntent)
            .setAutoCancel(true)
            .setTimeoutAfter(TIMEOUT_MS)
            .setSound(RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION))
            .setVibrate(new long[] { 0, 700, 400, 700 })
            .build();
        // Zvuk i vibracija se ponavljaju dok se obaveštenje ne otvori ili ukloni
        notification.flags |= Notification.FLAG_INSISTENT;

        manager.notify(tag, NOTIFICATION_ID, notification);
    }

    private static void ensureChannel(NotificationManager manager) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O || manager.getNotificationChannel(CHANNEL_ID) != null) return;

        NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "Nove porudžbine (zvono)", NotificationManager.IMPORTANCE_HIGH);
        channel.setDescription("Zvoni dok ne prihvatite ili odbijete novu porudžbinu");
        channel.enableVibration(true);
        channel.setVibrationPattern(new long[] { 0, 700, 400, 700 });
        channel.setLockscreenVisibility(Notification.VISIBILITY_PUBLIC);
        channel.setSound(
            RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION),
            new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_NOTIFICATION_RINGTONE)
                .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                .build());
        manager.createNotificationChannel(channel);
    }

    // Kad korisnik otvori aplikaciju, zvono preuzima aplikacija, pa se nativna zvona gase
    static void cancelAllAlarms(Context context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return;
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager == null) return;
        for (StatusBarNotification sbn : manager.getActiveNotifications()) {
            if (CHANNEL_ID.equals(sbn.getNotification().getChannelId())) manager.cancel(sbn.getTag(), sbn.getId());
        }
    }
}
